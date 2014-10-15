using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

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

            // TODO work out a way that these MemoryStreams (or the underlaying Byte []) can be pooled)
            // Maybe just use a ThreadStatic and just call clear/reset in-between each usage
            // Or use something like this https://gist.github.com/mganss/4434399
            var memoryStream = new MemoryStream(2000);
            var pdbMemoryStream = new MemoryStream(2000);

            // Write them to disk for debugging
            var diskEmitTimer = Stopwatch.StartNew();
            var test = compilation.Emit("test.dll", "test.pdb", "test.xml");
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

            // Trying out code from https://roslyn.codeplex.com/discussions/545394
            FileStream file;
            try
            {
                file = new FileStream("test.dll", FileMode.Open, FileAccess.Read, FileShare.Read);
                //using (PEReader peReader = new PEReader(memoryStream, PEStreamOptions.LeaveOpen))
                using (PEReader peReader = new PEReader(file))
                {
                    var mdReader = peReader.GetMetadataReader();
                    //PrintMethodSignatures(mdReader, namespaceName: "System.Collections.Generic", typeName: "Dictionary`2", methodName: ".ctor");
                    PrintMethodSignatures(mdReader, namespaceName: "", typeName: "TestingResharperMemoryPlugin", methodName: "TestingResharperMemoryAllocationPlugin");
                }
            }
            catch (BadImageFormatException e)
            {
                Console.WriteLine("Invalid PE image format: {0}\n", e.Message);
                //return 1;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error reading {0}: {1}", "test.dll", e.Message);
                //return 1;
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
            var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(memoryStream, readerParameters);
            readTimer.Stop();

            var ilInstructions = CodeGenerationHelper.GetILInstructionsFromAssembly(assembly);

            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(string.Join("\n", ilInstructions.Keys) + "\n");
            Console.ForegroundColor = origColour;

            var lines = root.GetText().Lines;

            var ilCodegroups = CodeGenerationHelper.ProcessIL(ilInstructions, lines);

            DebugHelper.PrintProcessedILInstructions(ilCodegroups, origColour);

            Console.WriteLine("\n\n\n");
            //PrintILInstructions(ilInstructions, origColour);
            //PrintILInstructionsAlongsideCode(ilInstructions, origColour, lines);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine();
            var format = "{0,-16} ({1,8:N2} ms)";
            Console.WriteLine("Parse:               " + format, parseTimer.Elapsed, parseTimer.Elapsed.TotalMilliseconds);
            Console.WriteLine("Compilation Create:  " + format, compilationCreateTimer.Elapsed, compilationCreateTimer.Elapsed.TotalMilliseconds);
            Console.WriteLine("Disk emit:           " + format, diskEmitTimer.Elapsed, diskEmitTimer.Elapsed.TotalMilliseconds);
            Console.WriteLine("Emit:                " + format, emitTimer.Elapsed, emitTimer.Elapsed.TotalMilliseconds);
            Console.WriteLine("Emit2:               " + format, emit2Timer.Elapsed, emit2Timer.Elapsed.TotalMilliseconds);
            Console.WriteLine("Parse2:              " + format, parse2Timer.Elapsed, parse2Timer.Elapsed.TotalMilliseconds);
            Console.WriteLine("Compilation Create2: " + format, compilationCreate2Timer.Elapsed, compilationCreate2Timer.Elapsed.TotalMilliseconds);
            Console.WriteLine("Emit3:               " + format, emit3Timer.Elapsed, emit3Timer.Elapsed.TotalMilliseconds);
            Console.WriteLine("Read:                " + format, readTimer.Elapsed, readTimer.Elapsed.TotalMilliseconds);
            Console.WriteLine();
            Console.ForegroundColor = origColour;
        }

        private static void PrintMethodSignatures(MetadataReader reader, string namespaceName, string typeName, string methodName)
        {
            System.Reflection.Metadata.TypeDefinition type;
            if (TryFindTypeDef(reader, namespaceName, typeName, out type))
            {
                foreach (var methodHandle in type.GetMethods())
                {
                    var method = reader.GetMethod(methodHandle);
                    if (reader.StringEquals(method.Name, methodName))
                    {
                        var signature = reader.GetBytes(method.Signature);
                        Console.WriteLine(reader.GetString(method.Name) + " " + BitConverter.ToString(signature));
                        //var handle = MethodImplementationHandle.FromRowId(method..id .currentRowId & 0xffffff);
                        //reader.GetMethodImplementation(.)
                    }
                }

                var methodImplementations = type.GetMethodImplementations().Count;

                //MethodImplementationHandleCollection handles = new MethodImplementationHandleCollection(reader, type);
                foreach (var methodImplHandle in type.GetMethodImplementations())
                {
                    var methodImpl = reader.GetMethodImplementation(methodImplHandle);
                    //if (reader.StringEquals(methodImpl., methodName))
                    //{
                    //    var bytes = reader.GetBytes(implementation.MethodBody);
                    //}
                }
            }
        }

        private static bool TryFindTypeDef(MetadataReader reader, string namespaceName, string typeName, out System.Reflection.Metadata.TypeDefinition type)
        {
            foreach (var typeHandle in reader.TypeDefinitions)
            {
                type = reader.GetTypeDefinition(typeHandle);
                if (reader.StringEquals(type.Name, typeName) && 
                    reader.StringEquals(type.Namespace, namespaceName))
                {
                    return true;
                }
            }

            type = default(System.Reflection.Metadata.TypeDefinition);
            return false;
        }
    }
}
