using Xunit;

namespace RoslynHeapAllocations.Tests
{
    public class StackOverflowAnswer : ScenarioTests
    {
        // Taken from http://stackoverflow.com/questions/7995606/boxing-occurrence-in-c-sharp
        [Fact]
        public void Converting_any_value_type_to_System_Object_type()
        {
            var @script = 
                    @"struct S { }
                    object box = new S();";
            var results = RunTest(script);
            AssertEx.ResultsContainAllocationType(results, AllocationType.Boxing, expectedLineNumber: 2);
        }

        [Fact]
        public void Converting_any_value_type_to_System_ValueType_type()
        {
            var @script = 
                    @"struct S { }
                    System.ValueType box = new S();";
            var results = RunTest(script);
            AssertEx.ResultsContainAllocationType(results, AllocationType.Boxing, expectedLineNumber: 2);
        }

        [Fact]
        public void Converting_any_enumeration_type_to_System_Enum_type()
        {
            var @script =
                @"enum E { A }
                System.Enum box = E.A;";
            var results = RunTest(script);
            AssertEx.ResultsContainAllocationType(results, AllocationType.Boxing, expectedLineNumber: 2);
        }

        [Fact]
        public void Converting_any_value_type_into_interface_reference()
        {
            var @script =
                @"interface I { }
                struct S : I { }
                I box = new S();";
            var results = RunTest(script);
            AssertEx.ResultsContainAllocationType(results, AllocationType.Boxing, expectedLineNumber: 3);
        }

        [Fact]
        public void Non_constant_value_types_in_CSharp_string_concatenation()
        {
            var @script =
                @"char c = 'c'; //F();
                string s1 = ""char value will box"" + c;";
            var results = RunTest(script);
            AssertEx.ResultsContainAllocationType(results, AllocationType.Boxing, expectedLineNumber: 2);
        }

        [Fact]
        public void Creating_delegate_from_value_type_instance_method()
        {
            var @script =
                @"using System;
                struct S { public void M() {} }
                Action box = new S().M;";
            var results = RunTest(script);
            AssertEx.ResultsContainAllocationType(results, AllocationType.Boxing, expectedLineNumber: 3);
        }

        [Fact]
        public void Calling_non_overridden_virtual_methods_on_value_types()
        {
            var @script =
                @"enum E { A }
                E.A.GetHashCode();";
            var results = RunTest(script);
            AssertEx.ResultsContainAllocationType(results, AllocationType.Boxing, expectedLineNumber: 2);
        }

        [Fact]
        public void Optional_parameters_of_object_type_with_value_type_default_values()
        {
            var @script =
                @"using System.Runtime.InteropServices;
                void M([Optional, DefaultParameterValue(42)] object o) {}
                M(); // boxing at call-site";
            var results = RunTest(script);
            AssertEx.ResultsContainAllocationType(results, AllocationType.Boxing, expectedLineNumber: 3);
        }
    }
}
