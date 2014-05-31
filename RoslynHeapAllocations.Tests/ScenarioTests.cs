using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Mono.Cecil;
using System.IO;
using Xunit;

namespace RoslynHeapAllocations.Tests
{
    public abstract class ScenarioTests
    {
        private static readonly ILCodeGroupComparer ILCodeGroupComparer = new ILCodeGroupComparer();

        public List<ILCodeGroup> RunTest(string script)
        {
            var origColour = Console.ForegroundColor;

            var options = new CSharpParseOptions(kind: SourceCodeKind.Script);
            var parseTimer = Stopwatch.StartNew();
            SyntaxTree tree = CSharpSyntaxTree.ParseText(script, options: options);
            parseTimer.Stop();

            var root = (CompilationUnitSyntax)tree.GetRoot();

            // Add all the references we need for the compilation
            var references = new List<MetadataReference>
            {
                new MetadataFileReference(typeof(System.Int32).Assembly.Location),
                new MetadataFileReference(typeof(System.Console).Assembly.Location),
                new MetadataFileReference(typeof(System.Linq.Enumerable).Assembly.Location),
                new MetadataFileReference(typeof(System.Collections.Generic.IList<>).Assembly.Location),
            };

            var compilationCreateTimer = Stopwatch.StartNew();
            var compilation = CSharpCompilation.Create("Test", new [] { tree }, references);
            compilationCreateTimer.Stop();

            var memoryStream = new MemoryStream(2000);
            var pdbMemoryStream = new MemoryStream(2000);

            var emitTimer = Stopwatch.StartNew();
            var result = compilation.Emit(memoryStream, pdbStream: pdbMemoryStream);
            emitTimer.Stop();

            var scriptDebugText = script + Environment.NewLine + Environment.NewLine;

            Assert.True(result.Success,
                string.Format("{0}Compilation of the script failed:{1}{2}", scriptDebugText, Environment.NewLine,
                                string.Join<Diagnostic>(Environment.NewLine, result.Diagnostics.ToArray())));

            var readerParameters = new ReaderParameters { ReadSymbols = true, SymbolStream = pdbMemoryStream };
            memoryStream.Position = 0;
            var readTimer = Stopwatch.StartNew();
            var assembly = AssemblyDefinition.ReadAssembly(memoryStream, readerParameters);
            readTimer.Stop();

            var processingTimer = Stopwatch.StartNew();
            var ilInstructions = CodeGenerationHelper.GetILInstructionsFromAssembly(assembly);

            var scriptCodeLocation = "System.Void Script::.ctor()";
            if (ilInstructions.Count == 0 || ilInstructions.ContainsKey(scriptCodeLocation) == false)
            {
                // Write them to disk for debugging
                var name = "Test-" + DateTime.Now.Ticks.ToString();
                var tempPath = Path.Combine(Path.GetTempPath(), name);
                var test = compilation.Emit(tempPath + ".dll", tempPath + ".pdb", tempPath + ".xml");
                var msg =
                    string.Format(
                        "{0} Failed to find any IL instructions in the assembly - saved as {1} - Emit Success: {2}",
                        scriptDebugText, tempPath, test.Success);
                AssertEx.Fail(msg);
            }

            var lines = root.GetText().Lines;
            foreach (var line in lines)
            {
                Console.WriteLine("[{0,4}] {1}", line.LineNumber + 1, line.ToString());
            }
            Console.WriteLine();

            var items = new List<ILCodeGroup>();
            CodeGenerationHelper.ProcessInstructionGroup(lines, ilInstructions[scriptCodeLocation], scriptCodeLocation, items);

            // Finally sort the items into the correct order and detect any allocations within the ILInstructions
            items.Sort(ILCodeGroupComparer);
            foreach (var codeGroup in items)
            {
                codeGroup.Allocation = AllocationDetector.DetectCodeGroupAllocations(codeGroup.ILInstructions);
            }
            processingTimer.Stop();

            DebugHelper.PrintProcessedILInstructions(items, origColour);

            Console.WriteLine("\n");
            Console.WriteLine("Parse:               {0} ({1:0.00} ms)", parseTimer.Elapsed, parseTimer.ElapsedMilliseconds);
            Console.WriteLine("Compilation Create:  {0} ({1:0.00} ms)", compilationCreateTimer.Elapsed, compilationCreateTimer.ElapsedMilliseconds);
            Console.WriteLine("Emit:                {0} ({1:0.00} ms)", emitTimer.Elapsed, emitTimer.ElapsedMilliseconds);
            Console.WriteLine("Read:                {0} ({1:0.00} ms)", readTimer.Elapsed, readTimer.ElapsedMilliseconds);
            Console.WriteLine("Processing:          {0} ({1:0.00} ms)", processingTimer.Elapsed, processingTimer.ElapsedMilliseconds);
            Console.WriteLine();

            return items;
        }
    }
}
