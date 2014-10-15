using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Mono.Cecil;
using RoslynHeapAllocations;
using RoslynHeapAllocations.VSAddin;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using Microsoft.CodeAnalysis.Emit;

namespace ConvertToAutoPropertyCS
{
    [DiagnosticAnalyzer]
    [ExportDiagnosticAnalyzer("ShowHiddenAllocations", LanguageNames.CSharp)]
    internal class CodeDiagnosticAnalyzer : ISyntaxTreeAnalyzer
    {
        internal static DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor("Testing123", "Memory alloction or boxing", "Hidden Allocation {0} {1}\n{2}", "Performance", DiagnosticSeverity.Warning);

        // SourceCodeKind.Interactive - "(7,1): error CS7021: You cannot declare namespace in script code"
        // SourceCodeKind.Regular - "error CS5001: Program does not contain a static 'Main' method suitable for an entry point"
        // SourceCodeKind.Script - "(7,1): error CS7021: You cannot declare namespace in script code" 
        private static CSharpParseOptions Options = new CSharpParseOptions(kind: SourceCodeKind.Script);

        // Add all the references we need for the compilation
        private List<MetadataReference> references = new List<MetadataReference>
            {
                new MetadataFileReference(typeof(System.Int32).Assembly.Location),
                new MetadataFileReference(typeof(System.Console).Assembly.Location),
                new MetadataFileReference(typeof(System.Linq.Enumerable).Assembly.Location),
                new MetadataFileReference(typeof(System.Collections.Generic.IList<>).Assembly.Location),
                new MetadataFileReference(typeof(System.Runtime.InteropServices.DefaultParameterValueAttribute).Assembly.Location),
            };

        // TODO either we need to specify up front the largest possible value, or me need to allow the MemoryStream 
        // arrays to grow, but use a Buffer Pool, so need to work out how best to do this
        private ThreadLocal<byte []> outputArray = new ThreadLocal<byte []>(() => new byte[20 * 1024]);
        private ThreadLocal<byte []> pdbArray = new ThreadLocal<byte []>(() => new byte [20 * 1024]);

        private bool PrintDebuggingInfo = false;
        //private bool PrintDebuggingInfo = true;

        public ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            //get { return ImmutableArray.Create(Rule); }
            get { return ImmutableArray.Create<DiagnosticDescriptor>(); }
        }

        public async void AnalyzeSyntaxTree(SyntaxTree tree, Action<Diagnostic> addDiagnostic, CancellationToken cancellationToken)
        {
            try
            {
                var overallTimer = Stopwatch.StartNew();
                var parseTimer = Stopwatch.StartNew();
                int insertedCharacters;
                SyntaxNode root = await tree.GetRootAsync(cancellationToken);
                SyntaxTree newTree = ParseAndTidyUpCode(root, cancellationToken, out insertedCharacters);
                var newRoot = await newTree.GetRootAsync(cancellationToken);
                parseTimer.Stop();

                var compilationCreateTimer = Stopwatch.StartNew();
                var compilation = CSharpCompilation.Create("ShowHiddenAllocations", new[] { newTree }, references);
                compilationCreateTimer.Stop();

                // Write them to disk for debugging
                //var diskEmitTimer = Stopwatch.StartNew();
                ////Logger.Log("Current directory: " + Environment.CurrentDirectory);
                ////var test = compilation.Emit("ShowHiddenAllocations.dll", "ShowHiddenAllocations.pdb", "ShowHiddenAllocations.xml");
                //diskEmitTimer.Stop();

                var emitTimer = Stopwatch.StartNew();
                Array.Clear(outputArray.Value, 0, outputArray.Value.Length);
                var outputStream = new MemoryStream(outputArray.Value);
                Array.Clear(pdbArray.Value, 0, pdbArray.Value.Length);
                var pdbStream = new HackedMemoryStream(pdbArray.Value);
                var result = compilation.Emit(outputStream, pdbStream: pdbStream, cancellationToken: cancellationToken);
                emitTimer.Stop();
                Logger.Log("Emit result: {0}\n\t{1}", result.Success, string.Join("\n\t", result.Diagnostics));

                if (PrintDebuggingInfo)
                {
                    Logger.Log("Output position = {0} (length = {1}), pdb position = {2} (length = {3})",
                                outputStream.Position, outputStream.Length, pdbStream.Position, pdbStream.Length);
                }

                var newLines = newRoot.GetText().Lines;
                if (result.Success == false)
                {
                    PrintCompilationErrors(result, newLines);
                    return;
                }

                if (PrintDebuggingInfo)
                {
                    PrintBeforeAfterCodeInfo(root, newRoot, newLines);
                }

                var readerParameters = new ReaderParameters { ReadSymbols = true, SymbolStream = pdbStream };
                var readTimer = Stopwatch.StartNew();
                outputStream.Position = 0;
                var assembly = AssemblyDefinition.ReadAssembly(outputStream, readerParameters);
                readTimer.Stop();

                var processingTimer = Stopwatch.StartNew();
                var ilInstructions = CodeGenerationHelper.GetILInstructionsFromAssembly(assembly);
                var ilCodegroups = CodeGenerationHelper.ProcessIL(ilInstructions, newLines);
                processingTimer.Stop();

                if (PrintDebuggingInfo)
                {
                    PrintILInstructionsWithAllocationsInfo(ilInstructions, ilCodegroups);
                }

                var diagnostics = CreateDiagnostics(tree, insertedCharacters, newLines, ilCodegroups);
                foreach (var diagnostic in diagnostics)
                {
                    addDiagnostic(diagnostic);
                }

                overallTimer.Stop();
                Logger.Log();
                var format = "{0,-16} ({1,8:N2} ms)";
                Logger.Log("Parse:               " + format, parseTimer.Elapsed, parseTimer.Elapsed.TotalMilliseconds);
                Logger.Log("Compilation Create:  " + format, compilationCreateTimer.Elapsed, compilationCreateTimer.Elapsed.TotalMilliseconds);
                //Logger.Log("Disk emit:           " + format, diskEmitTimer.Elapsed, diskEmitTimer.Elapsed.TotalMilliseconds);
                Logger.Log("Emit:                " + format, emitTimer.Elapsed, emitTimer.Elapsed.TotalMilliseconds);
                Logger.Log("Read:                " + format, readTimer.Elapsed, readTimer.Elapsed.TotalMilliseconds);
                Logger.Log("Processing:          " + format, processingTimer.Elapsed, processingTimer.Elapsed.TotalMilliseconds);
                Logger.Log("---------------------");
                Logger.Log("Overall:             " + format, overallTimer.Elapsed, overallTimer.Elapsed.TotalMilliseconds);
                Logger.Log();
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message);
                Logger.Log(ex.ToString());
            }
        }

        private SyntaxTree ParseAndTidyUpCode(SyntaxNode root, CancellationToken cancellationToken, out int insertedCharacters)
        {
            //TODO this feels HACKY, why is it 1 when we DON'T modify the text?!!!
            insertedCharacters = 1;

            if (root.ChildNodes().Any(n => n.CSharpKind() == SyntaxKind.NamespaceDeclaration))
            {
                var program = StringBuilderCache.AcquireBuilder();
                foreach (var node in root.ChildNodes())
                {
                    if (node.CSharpKind() == SyntaxKind.NamespaceDeclaration)
                    {
                        // TODO This is still NOT ROBUST, it assumes the curly braces are on seperate lines, which might not be the case!!
                        // TODO The main thing we need to do is keep the original line offsets, so that the line numbers are consistent!!!
                        var namespaceNode = (NamespaceDeclarationSyntax)node;
                        var leadingTrivia = namespaceNode.NamespaceKeyword.LeadingTrivia.ToFullString();
                        if (string.IsNullOrEmpty(leadingTrivia) == false)
                            program.Append(leadingTrivia);

                        program.AppendFormat("//{0} {1}\n", namespaceNode.NamespaceKeyword.ToString(), namespaceNode.Name);
                        program.AppendLine("//{"); // (node.ToFullString());
                        program.Append(namespaceNode.Members.ToFullString());
                        program.AppendLine("//}"); // (node.ToFullString());

                        // TODO this is hard-coded and NOT robust, but we need to allowed for the "//" characters we inserted!!!
                        insertedCharacters = 4;
                    }
                    else
                    {
                        program.Append(node.ToFullString());
                    }
                }

                return CSharpSyntaxTree.ParseText(StringBuilderCache.GetStringAndReleaseBuilder(program), options: Options, cancellationToken: cancellationToken);
            }
            else
            {
                return CSharpSyntaxTree.ParseText(root.GetText().ToString(), options: Options, cancellationToken: cancellationToken);
            }
        }

        private static List<Diagnostic> CreateDiagnostics(SyntaxTree tree, int insertedCharacters, TextLineCollection newLines, List<ILCodeGroup> ilCodegroups)
        {
            var diagnostics = new List<Diagnostic>(50);
            foreach (var group in ilCodegroups)
            {
                if (group.Allocation == AllocationType.None)
                    continue;

                foreach (var instruction in group.ILInstructions)
                {
                    var locn = DebugHelper.SequencePointToString(instruction.SequencePoint);
                    var location = instruction.SequencePoint;
                    if (group.Allocation != AllocationType.None && location != null &&
                        location.StartLine != CodeGenerationHelper.HiddenLocation &&
                        location.EndLine != CodeGenerationHelper.HiddenLocation)
                    {
                        int start = 0, end = 0;
                        if (location.StartLine != location.EndLine) // && group.Allocation == AllocationType.GetEnumerator)
                        {
                            // As a simple "hack", just put the location as the first line, multi-line looks wierd
                            // in VS as it can overlap with single-line ones and things become unclear!
                            start = newLines[location.StartLine - 1].Start + location.StartColumn - insertedCharacters;
                            end = newLines[location.StartLine - 1].Start + location.EndColumn - insertedCharacters;
                        }
                        else
                        {
                            start = newLines[location.StartLine - 1].Start + location.StartColumn - insertedCharacters;
                            end = newLines[location.EndLine - 1].Start + location.EndColumn - insertedCharacters;
                        }

                        var span = new TextSpan(start: start, length: end - start);
                        // This will no longer be printed "in-line" with the IL, so maybe it's a bit redundant printing it out?!?
                        //if (PrintDebuggingInfo) 
                        //    Logger.Log("Creating span: {0} -> {1} (length = {2}) (insertedCharacters = {3})", start, end, end - start, insertedCharacters);
                        var diagnosticLocation = Location.Create(tree, span);
                        var diagnostic = Diagnostic.Create(Rule, diagnosticLocation,
                                                    group.Allocation, /*group.AllocationExplanation,*/ locn, string.Join("\n", group.ILInstructions));
                        diagnostics.Add(diagnostic);
                    }
                }
            }

            return diagnostics;
        }

        private static void PrintCompilationErrors(EmitResult result, TextLineCollection newLines)
        {
            Logger.Log("There were errors when compiling the code:");
            foreach (var line in newLines)
            {
                Logger.Log("[{0,4}] {1}", line.LineNumber + 1, line.ToString());
            }
            Logger.Log();
            foreach (var diagnostic in result.Diagnostics)
            {
                Logger.Log("\t" + diagnostic.ToString());
            }
        }

        private static void PrintILInstructionsWithAllocationsInfo(Dictionary<string, Collection<Instruction>> ilInstructions, List<ILCodeGroup> ilCodegroups)
        {
            Logger.Log("All instructions:");
            foreach (var instruction in ilInstructions.Keys)
            {
                Logger.Log("\t" + instruction.ToString());
            }
            Logger.Log();

            foreach (var group in ilCodegroups)
            {
                if (group.Allocation == AllocationType.None)
                    continue;

                Logger.Log(group.ClassName);
                if (string.IsNullOrWhiteSpace(group.DebugText) == false)
                    Logger.Log(group.DebugText);

                if (group.Allocation != AllocationType.None)
                {
                    Logger.Log("### HIDDEN ALLOCATION: {0} ###", group.Allocation.ToString());
                }

                foreach (var instruction in group.ILInstructions)
                {
                    Logger.LogWithoutNewLine(instruction.ToString());
                    var locn = DebugHelper.SequencePointToString(instruction.SequencePoint);
                    if (string.IsNullOrWhiteSpace(locn) == false)
                        Logger.Log(locn);
                }

                Logger.Log();
            }
        }

        private static void PrintBeforeAfterCodeInfo(SyntaxNode root, SyntaxNode newRoot, TextLineCollection newLines)
        {
            Logger.Log("\nOriginal code had {0} lines ({1} chars)\nModified code has {2} lines ({3} chars)",
                    root.GetText().Lines.Count, root.GetText().Length, newLines.Count, newRoot.GetText().Length);

            Logger.Log("Successfully parsed the code");
            Logger.Log("\nORIGINAL CODE");
            foreach (var line in root.GetText().Lines)
            {
                Logger.Log("[{0,4}] {1}", line.LineNumber + 1, line.ToString());
            }
            Logger.Log("\nMODIFIED CODE");
            foreach (var line in newLines)
            {
                Logger.Log("[{0,4}] {1}", line.LineNumber + 1, line.ToString());
            }
            Logger.Log();

            var originalLines = root.GetText().Lines;
            for (int i = 0; i < Math.Min(newLines.Count, originalLines.Count); i++)
            {
                if (newLines[i].ToString() != originalLines[i].ToString())
                {
                    Logger.Log("Difference:");
                    Logger.Log("[{0,4}] <{1}>", i + 1, originalLines[i].ToString().Replace('\n', '@'));
                    Logger.Log("[{0,4}] <{1}>", i + 1, newLines[i].ToString().Replace('\n', '@'));
                }
            }
        }
    }
}