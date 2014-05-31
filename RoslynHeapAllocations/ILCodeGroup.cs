using System.Collections.Generic;
using Mono.Cecil.Cil;

namespace RoslynHeapAllocations
{
    public class ILCodeGroup
    {
        public List<Instruction> ILInstructions { get; set; }
        public string DebugText { get; set; }
        public SequencePoint Location { get; set; }
        public string ClassName { get; set; }
        public AllocationType Allocation { get; set; }
        public string AllocationExplanation { get; set; }
    }
}
