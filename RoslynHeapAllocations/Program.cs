using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RoslynHeapAllocations
{
    class Program
    {
        private static void Main(string[] args)
        {
            //RunTest(CodeSamples.sampleProgram1);

            // Run a copy of TestingResharperMemoryPlugin.cs
            RunTest(CodeSamples.sampleProgram2, new CSharpParseOptions(kind: SourceCodeKind.Script));
        }

        private static void RunTest(string program, CSharpParseOptions options = null)
        {
            var origColour = Console.ForegroundColor;

            var parseTimer = Stopwatch.StartNew();
            SyntaxTree tree = CSharpSyntaxTree.ParseText(program, options: options);
            parseTimer.Stop();

            //var testingConst = 5;
            var root = (CompilationUnitSyntax)tree.GetRoot();

            //Console.ForegroundColor = ConsoleColor.DarkYellow;
            //foreach (var line in root.GetText().Lines)
            //{
            //    Console.WriteLine("[{0,4}] {1}", line.LineNumber + 1, line.ToString());
            //}
            //Console.ForegroundColor = origColour;
            //Console.WriteLine();

            // Add all the references we need for the compilation
            var references = new List<MetadataReference>
            {
                new MetadataFileReference(typeof(int).Assembly.Location),
                new MetadataFileReference(typeof(Console).Assembly.Location),
                new MetadataFileReference(typeof(Enumerable).Assembly.Location),
                new MetadataFileReference(typeof(IList<>).Assembly.Location)
            };

            var compilationCreateTimer = Stopwatch.StartNew();
            var compilation = CSharpCompilation.Create("Test", new [] { tree }, references);
            compilationCreateTimer.Stop();

            var memoryStream = new MemoryStream(2000);
            var pdbMemoryStream = new MemoryStream(2000);
            // Write them to disk for debugging
            var diskEmitTimer = Stopwatch.StartNew();
            //var test = compilation.Emit("test.dll", "test.pdb", "test.xml");
            diskEmitTimer.Stop();
            var emitTimer = Stopwatch.StartNew();
            var result = compilation.Emit(memoryStream, pdbStream: pdbMemoryStream);
            emitTimer.Stop();
            if (result.Success == false)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                foreach (var diagnostic in result.Diagnostics)
                {
                    Console.WriteLine(diagnostic);
                }
                Console.ForegroundColor = origColour;
                return;
            }

            var memoryStream2 = new MemoryStream(2000);
            var pdbMemoryStream2 = new MemoryStream(2000);
            var emit2Timer = Stopwatch.StartNew();
            var result2 = compilation.Emit(memoryStream2, pdbStream: pdbMemoryStream2);
            emit2Timer.Stop();

            var parse2Timer = Stopwatch.StartNew();
            var tree2 = CSharpSyntaxTree.ParseText(CodeSamples.sampleProgram1 + Environment.NewLine + "//", options: options);
            parse2Timer.Stop();
            var compilationCreate2Timer = Stopwatch.StartNew();
            var compilation2 = CSharpCompilation.Create("Test", new[] { tree2 }, references);
            compilationCreate2Timer.Stop();
            var memoryStream3 = new MemoryStream(2000);
            var pdbMemoryStream3 = new MemoryStream(2000);
            var emit3Timer = Stopwatch.StartNew();
            var result3 = compilation2.Emit(memoryStream3, pdbStream: pdbMemoryStream3);
            emit3Timer.Stop();

            var readerParameters = new ReaderParameters {ReadSymbols = true, SymbolStream = pdbMemoryStream};
            var readTimer = Stopwatch.StartNew();
            memoryStream.Position = 0;
            var assembly = AssemblyDefinition.ReadAssembly(memoryStream, readerParameters);
            readTimer.Stop();

            var ilInstructions = CodeGenerationHelper.GetILInstructionsFromAssembly(assembly);

            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(string.Join("\n", ilInstructions.Keys) + "\n");
            Console.ForegroundColor = origColour;

            var lines = root.GetText().Lines;

            var ilCodegroups = CodeGenerationHelper.ProcessIL(ilInstructions, lines);

            //DebugHelper.PrintProcessedILInstructions(ilCodegroups, origColour);
            DebugHelper.PrintProcessedILInstructions(ilCodegroups, origColour);

            Console.WriteLine("\n\n\n");
            //PrintILInstructions(ilInstructions, origColour);
            //PrintILInstructionsAlongsideCode(ilInstructions, origColour, lines);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine();
            Console.WriteLine("Parse:               {0} ({1:0.00} ms)", parseTimer.Elapsed, parseTimer.ElapsedMilliseconds);
            Console.WriteLine("Compilation Create:  {0} ({1:0.00} ms)", compilationCreateTimer.Elapsed, compilationCreateTimer.ElapsedMilliseconds);
            Console.WriteLine("Disk emit:           {0} ({1:0.00} ms)", diskEmitTimer.Elapsed, diskEmitTimer.ElapsedMilliseconds);
            Console.WriteLine("Emit:                {0} ({1:0.00} ms)", emitTimer.Elapsed, emitTimer.ElapsedMilliseconds);
            Console.WriteLine("Emit2:               {0} ({1:0.00} ms)", emit2Timer.Elapsed, emit2Timer.ElapsedMilliseconds);
            Console.WriteLine("Parse2:              {0} ({1:0.00} ms)", parse2Timer.Elapsed, parse2Timer.ElapsedMilliseconds);
            Console.WriteLine("Compilation Create2: {0} ({1:0.00} ms)", compilationCreate2Timer.Elapsed, compilationCreate2Timer.ElapsedMilliseconds);
            Console.WriteLine("Emit3:               {0} ({1:0.00} ms)", emit3Timer.Elapsed, emit3Timer.ElapsedMilliseconds);
            Console.WriteLine("Read:                {0} ({1:0.00} ms)", readTimer.Elapsed, readTimer.ElapsedMilliseconds);
            Console.WriteLine();
            Console.ForegroundColor = origColour;
        }
    }
}
