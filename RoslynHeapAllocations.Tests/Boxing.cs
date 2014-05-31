using System.Linq;
using Xunit;
using Xunit.Extensions;

namespace RoslynHeapAllocations.Tests
{
    public class Boxing : ScenarioTests
    {
        [Fact]
        public void String_format_value_type_object_params_Boxing()
        {
            var script =
                @"var test = string.Format(""blah {0}"", 5, 'b', ""d""); // Boxing on 5 and 'b' (Resharper plugin)";
            var results = RunTest(script);
            Assert.True(results.Any(r => r.Allocation == AllocationType.Boxing));
        }

        [Fact]
        public void String_format_reference_type_object_params_NO_Boxing()
        {
            var script =
                @"var test = string.Format(""blah {0}"", 5.ToString(), ""b"", ""d"");";
            var results = RunTest(script);
            AssertEx.AllResultsAreAllocationTypeNone(results);
        }

        [Fact]
        public void String_concatenation_value_type_Boxing()
        {
            var script =
                @"var withBoxing = 5.ToString() + ':' + 8.ToString(); // Boxing on ':' (Resharper plugin)";
            var results = RunTest(script);
            Assert.True(results.Any(r => r.Allocation == AllocationType.Boxing));
        }

        [Fact]
        public void String_concatenation_all_strings_NO_Boxing()
        {
            var script =
                @"var withoutBoxing = 5.ToString() + "":"" + 8.ToString();";
            var results = RunTest(script);
            AssertEx.AllResultsAreAllocationTypeNone(results);
        }

        [Fact]
        public void Enum_GetHashCode_Boxing()
        {
            var script =
                @"public enum Color { Red, Green, Blue }
                Color colour = Color.Red;
                var enumBoxing = colour.GetHashCode(); // Boxing (Resharper plugin)";
            var results = RunTest(script);
            Assert.True(results.Any(r => r.Allocation == AllocationType.Boxing));
        }

        [Fact]
        public void Enum_Cast_to_int_GetHashCode_NO_Boxing()
        {
            var script =
                @"public enum Color { Red, Green, Blue }
                Color colour = Color.Red;
                var fixedEnumBoxing = ((int)colour).GetHashCode();";
            var results = RunTest(script);
            AssertEx.AllResultsAreAllocationTypeNone(results);
        }

        [Fact]
        public void String_GetHashCode_NO_Boxing()
        {
            var script =
                @"var noBoxing = ""test"".GetHashCode(); // NO boxing (Resharper plugin)";
            var results = RunTest(script);
            AssertEx.AllResultsAreAllocationTypeNone(results);
        }

        [Fact]
        public void Struct_not_overriding_GetHashCode_Boxing()
        {
            var script =
                @"public struct NoGetHashCode
                {
                    public readonly int Test;
                    public NoGetHashCode(int test) { Test = test; }
                }
                var noGetHashCode = new NoGetHashCode(5);
                var noGetHashCodeHC = noGetHashCode.GetHashCode(); // Boxing (ResharperPlugin) because GetHashCode is NOT overridden";
            var results = RunTest(script);
            AssertEx.ResultsContainAllocationType(results, AllocationType.Boxing, expectedLineNumber: 7);
        }

        [Fact]
        public void Struct_overriding_GetHashCode_NO_Boxing()
        {
            var script =
                @"public struct WithGetHashCode
                {
                    public readonly int Test;
                    public WithGetHashCode(int test) { Test = test; }
                    public override int GetHashCode() { return Test.GetHashCode(); }
                    public int GetHashCodeNormal() { return Test.GetHashCode(); }
                }
                var withGetHashCode = new WithGetHashCode(5);
                var withGetHashCodeHC = withGetHashCode.GetHashCode(); // NO Boxing (ResharperPlugin) because GetHashCode IS overridden
                var withGetHashCodeHCNormal = withGetHashCode.GetHashCodeNormal();";
            var results = RunTest(script);
            AssertEx.ResultsContainAllocationType(results, AllocationType.New, expectedLineNumber: 8);
            AssertEx.ResultsContainAllocationType(results, AllocationType.None, expectedLineNumber: 9);
            AssertEx.ResultsContainAllocationType(results, AllocationType.None, expectedLineNumber: 10);
        }

        [Fact]
        public void IComparable_Boxing_and_NO_Boxing()
        {
            var script =
                @"using System;
                int number = 5;

                // CompareTo is found on the IComparable interface
                IComparable comparable = number; // box number - boxing (Resharper plugin)
                int result3 = comparable.CompareTo(8); // box 8 - Boxing (Resharper plugin)

                // CompareTo is also a public method on int directly. In fact two overloads of CompareTo are available
                // on int, one that takes System.Object and another that takes an int, so the C# compiler will choose
                // to call the one that takes int, avoiding both boxes that the above example causes.
                int result4 = number.CompareTo(8); // NO boxing (Resharper plugin)";
            var results = RunTest(script);
            AssertEx.ResultsContainAllocationType(results, AllocationType.Boxing, expectedLineNumber: 5);
            AssertEx.ResultsContainAllocationType(results, AllocationType.Boxing, expectedLineNumber: 6);
            AssertEx.ResultsContainAllocationType(results, AllocationType.None, expectedLineNumber: 11);
        }
    }
}
