using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace RoslynHeapAllocations.Tests
{
    public static class AssertEx
    {
        public static void Fail(string message)
        {
            Assert.True(false, message);
        }

        public static void AllResultsAreAllocationTypeNone(List<ILCodeGroup> results)
        {
            var msg = string.Format(
                "Expected all results to be \"AllocationType.None\", instead got :[{0}]",
                string.Join(", ", results.Select(r => new { r.Allocation, Line = r.Location.StartLine })));
            Assert.True(results.All(r => r.Allocation == AllocationType.None), msg);
        }

        public static void ResultsContainAllocationType(List<ILCodeGroup> results, 
                                    AllocationType expectedAllocationType, 
                                    int? expectedLineNumber = null, 
                                    int expectedOccurencesOnLine = 1)
        {
            if (expectedLineNumber == null)
            {
                var msg = string.Format(
                    "Expected results to contain \"AllocationType.{0}\", instead got :[{1}]",
                    expectedAllocationType, 
                    string.Join(", ", results.Select(r => new { r.Allocation, Line = r.Location.StartLine })));
                Assert.True(results.Any(r => r.Allocation == expectedAllocationType), msg);
            }
            else
            {
                var resultsOnLine = results.Where(r => r.Location.StartLine == expectedLineNumber &&
                                                       r.Allocation == expectedAllocationType)
                    .ToList();
                var msg = string.Format(
                    "Expected results to contain {0} occurences of \"AllocationType.{1}\" on line {2}, instead got {3} :[{4}]",
                    expectedOccurencesOnLine,
                    expectedAllocationType,
                    expectedLineNumber,
                    resultsOnLine.Count,
                    string.Join(", ", results.Select(r => new {r.Allocation, Line = r.Location.StartLine})));
                Assert.True(expectedOccurencesOnLine == resultsOnLine.Count, msg);
            }
        }
    }
}