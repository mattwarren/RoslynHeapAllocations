using Microsoft.CodeAnalysis.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using System;
using System.Collections.Generic;

namespace RoslynHeapAllocations
{
    public static class CodeGenerationHelper
    {
        // See http://mono.1490590.n4.nabble.com/Trouble-with-SequencePoint-td1550909.html#a1550910
        // and http://blogs.msdn.com/b/jmstall/archive/2005/06/19/feefee-sequencepoints.aspx
        public static readonly int HiddenLocation = 16707566;

        private static readonly ILCodeGroupComparer ILCodeGroupComparer = new ILCodeGroupComparer();

        public static Dictionary<string, Collection<Instruction>> GetILInstructionsFromAssembly(AssemblyDefinition assembly)
        {
            // Probably need to do this properly, i.e.
            // For each Type inside assembly, in those look for methods, etc
            // Also look for the methods at the top level, etc!!
            var ilInstructions = new Dictionary<string, Collection<Instruction>>();
            var script = assembly.MainModule.GetType("Script"); // This is the class that Roslyn puts everything in
            if (script != null)
            {
                foreach (var method in script.Methods)
                {
                    ilInstructions.Add(method.FullName, method.Body.Instructions);
                }

                foreach (var type in script.NestedTypes)
                {
                    foreach (var method in type.Methods)
                    {
                        ilInstructions.Add(method.FullName, method.Body.Instructions);
                    }

                    foreach (var nestedType in type.NestedTypes)
                    {
                        foreach (var method in nestedType.Methods)
                        {
                            ilInstructions.Add(method.FullName, method.Body.Instructions);
                        }
                    }
                }
            }
            else
            {
                var ilProcessor = assembly.MainModule.EntryPoint.Body.GetILProcessor();
                ilInstructions.Add(assembly.MainModule.EntryPoint.FullName, ilProcessor.Body.Instructions);
            }
            return ilInstructions;
        }

        public static List<ILCodeGroup> ProcessIL(Dictionary<string, Collection<Instruction>> ilInstructions, TextLineCollection allLines)
        {
            var items = new List<ILCodeGroup>();
            foreach (var pair in ilInstructions)
            {
                ProcessInstructionGroup(allLines, pair.Value, pair.Key, items);
            }

            // Finally sort the items into the correct order and detect any allocations within the ILInstructions
            items.Sort(ILCodeGroupComparer);
            foreach (var codeGroup in items)
            {
                codeGroup.Allocation = AllocationDetector.DetectCodeGroupAllocations(codeGroup.ILInstructions);
            }

            return items;
        }

        internal static void ProcessInstructionGroup(TextLineCollection allLines, Collection<Instruction> instructions, string className, List<ILCodeGroup> items)
        {
            ILCodeGroup ilGroup = null;
            foreach (var instruction in instructions)
            {
                var location = instruction.SequencePoint;
                if (location != null)
                {
                    // Ignore Hidden SequencePoints, only start a new ILCodeGroup with ones that have a location!!
                    if (location.StartLine != HiddenLocation && location.EndLine != HiddenLocation)
                    {
                        var stringBuilder = StringBuilderCache.AcquireBuilder();
                        for (int i = location.StartLine - 1; i < location.EndLine; i++)
                        {
                            var line = allLines[i];
                            stringBuilder.AppendFormat("[{0,4}] {1}{2}", 
                                        (line.LineNumber + 1).ToString(), line.ToString(), Environment.NewLine);
                        }

                        stringBuilder.AppendFormat("{0}{1}",
                            new string(' ', 7 + location.StartColumn - 1),
                            new string('*', location.EndColumn - location.StartColumn));

                        ilGroup = new ILCodeGroup
                        {
                            ClassName = className,
                            ILInstructions = new List<Instruction>(),
                            Location = location,
                            DebugText = StringBuilderCache.GetStringAndReleaseBuilder(stringBuilder),
                        };
                        items.Add(ilGroup);
                    }
                }

                if (ilGroup != null)
                    ilGroup.ILInstructions.Add(instruction);
            }
        }
    }
}
