using System.Collections.Generic;

namespace RoslynHeapAllocations
{
    public class ILCodeGroupComparer : Comparer<ILCodeGroup>
    {
        public override int Compare(ILCodeGroup x, ILCodeGroup y)
        {
            if (x.Location.StartLine == y.Location.StartLine)
            {
                // Both Location's on the same line, sort on the Location Column
                return x.Location.StartColumn.CompareTo(y.Location.StartColumn);
            }
            else
            {
                // Different lines, sort on Location StartLine
                return x.Location.StartLine.CompareTo(y.Location.StartLine);
            }
        }
    }
}