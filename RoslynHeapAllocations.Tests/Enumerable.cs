using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace RoslynHeapAllocations.Tests
{
    public class Enumerable : ScenarioTests
    {
        [Fact]
        public void IEnumerable_GetEnumerator_Value_Type__NO_Boxing()
        {
             var script =
                @"using System;
                using System.Collections.Generic;
                using System.Linq;

                int[] intData = new[] { 123, 32, 4 };
                List<int> listData = new[] { 123, 32, 4 }.ToList();
                IList<int> iListData = new[] { 123, 32, 4 };

                foreach (var i in intData)
                {
                }

                foreach (var i in listData)
                {
                }";
            var results = RunTest(script);
            AssertEx.ResultsContainAllocationType(results, AllocationType.None, expectedLineNumber: 9, expectedOccurencesOnLine: 4);
            AssertEx.ResultsContainAllocationType(results, AllocationType.None, expectedLineNumber: 13, expectedOccurencesOnLine: 4);
        }

        [Fact]
        public void IEnumerable_GetEnumerator_Reference_Type_Boxing()
        {
            var script =
                @"using System;
                using System.Collections.Generic;
                using System.Linq;

                int[] intData = new[] { 123, 32, 4 };
                List<int> listData = new[] { 123, 32, 4 }.ToList();
                IList<int> iListData = new[] { 123, 32, 4 };

                foreach (var i in iListData) // Allocations (Resharper plugin)
                {
                }

                foreach (var i in (IEnumerable<int>)intData) // Allocations (Resharper plugin)
                {
                }";
            var results = RunTest(script);
            AssertEx.ResultsContainAllocationType(results, AllocationType.GetEnumerator, expectedLineNumber: 9, expectedOccurencesOnLine: 1);
            AssertEx.ResultsContainAllocationType(results, AllocationType.None, expectedLineNumber: 9, expectedOccurencesOnLine: 3);
            AssertEx.ResultsContainAllocationType(results, AllocationType.GetEnumerator, expectedLineNumber: 13, expectedOccurencesOnLine: 1);
            AssertEx.ResultsContainAllocationType(results, AllocationType.None, expectedLineNumber: 13, expectedOccurencesOnLine: 3);
        }
    }
}
