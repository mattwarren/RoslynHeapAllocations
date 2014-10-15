using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;

namespace RoslynHeapAllocations
{
    public static class DebugHelper
    {
        public static void PrintProcessedILInstructions(List<ILCodeGroup> ilCodegroups, ConsoleColor origColour)
        {
            foreach (var group in ilCodegroups)
            {
                //if (group.ILInstructions.All(i => i.OpCode == OpCodes.Nop))
                //    continue;
                //if (group.Allocation == AllocationType.None)
                //    continue;

                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine(group.ClassName);

                Console.ForegroundColor = ConsoleColor.DarkYellow;
                if (string.IsNullOrWhiteSpace(group.DebugText) == false)
                    Console.WriteLine(group.DebugText);

                //Console.ForegroundColor = ConsoleColor.Green;
                //Console.WriteLine(SequencePointToString(group.Location));

                if (group.Allocation != AllocationType.None)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(group.Allocation.ToString());
                }

                foreach (var instruction in group.ILInstructions)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write(instruction.ToString());
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(SequencePointToString(instruction.SequencePoint));
                }

                Console.ForegroundColor = origColour;
                Console.WriteLine();
            }
        }

        public static string SequencePointToString(SequencePoint location)
        {
            if (location == null)
                return String.Empty;

            if (location.StartLine == CodeGenerationHelper.HiddenLocation &&
                location.EndLine == CodeGenerationHelper.HiddenLocation)
            {
                return " @ HIDDEN (0xfeefee)";
            }

            return string.Format(" @ {0},{1} -> {2},{3}",
                location.StartLine.ToString(), location.StartColumn.ToString(),
                location.EndLine.ToString(), location.EndColumn.ToString());
        }

        private static void PrintILInstructions(
                            Dictionary<string, Collection<Instruction>> ilInstructions,
                            ConsoleColor origColour)
        {
            foreach (var pair in ilInstructions)
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine("\n" + pair.Key);
                Console.ForegroundColor = origColour;
                foreach (var instruction in pair.Value)
                {
                    var location = instruction.SequencePoint;
                    if (location != null)
                    {
                        Console.Write(instruction);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine(SequencePointToString(instruction.SequencePoint));
                        Console.ForegroundColor = origColour;
                    }
                    else
                    {
                        Console.WriteLine(instruction.ToString());
                    }
                }
            }
        }

        private static void PrintILInstructionsAlongsideCode(
                            Dictionary<string, Collection<Instruction>> ilInstructions,
                            ConsoleColor origColour,
                            TextLineCollection allLines)
        {
            foreach (var pair in ilInstructions)
            {
                SequencePoint lastLocation = null;
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine("\n" + pair.Key);
                Console.ForegroundColor = origColour;
                foreach (var instruction in pair.Value)
                {
                    var location = instruction.SequencePoint;
                    if (location == null)
                    {
                        Console.WriteLine(instruction.ToString());
                        continue;
                    }

                    //int startLine = 0;
                    int startLine = location.StartLine - 1;
                    if (lastLocation != null)
                        startLine = lastLocation.StartLine;

                    if (location.StartLine != CodeGenerationHelper.HiddenLocation && location.EndLine != CodeGenerationHelper.HiddenLocation)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        var linesToPrint = allLines.Skip(startLine).Take(location.EndLine - startLine).ToList();
                        foreach (var line in linesToPrint)
                        {
                            Console.WriteLine("[{0,4}] {1}", (line.LineNumber + 1).ToString(), line.ToString());
                        }
                        Console.WriteLine(new string(' ', 7 + location.StartColumn - 1) +
                                          new string('*', location.EndColumn - location.StartColumn));
                        Console.ForegroundColor = origColour;
                        lastLocation = location;
                    }

                    Console.Write(instruction);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(SequencePointToString(instruction.SequencePoint));
                    Console.ForegroundColor = origColour;
                }

                //if (lastLocation == null)
                //    continue;

                // Print out any remaining lines, since the last IL location
                //Console.ForegroundColor = ConsoleColor.DarkYellow;
                //var remainingLines = allLines.Skip(lastLocation.StartLine).ToList();
                //foreach (var line in remainingLines)
                //{
                //    Console.WriteLine("[{0,4}] {1}", line.LineNumber + 1, line.ToString());
                //}
                //Console.ForegroundColor = origColour;
            }
        }
    }
}
