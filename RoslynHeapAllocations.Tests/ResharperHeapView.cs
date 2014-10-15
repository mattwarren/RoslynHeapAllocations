using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace RoslynHeapAllocations.Tests
{
    // From https://github.com/controlflow/resharper-heapview
    public class ResharperHeapView : ScenarioTests
    {
        [Fact]
        public void All_boxing_scenarios()
        {
            var script =
                @"using System;
                  struct Boxing {
                        void M(string a) {
                            object obj = 42;                // Line  4: implicit conversion Int32 ~> Object
                            string path = a + '/' + obj;    // Line  5: implicit conversion Char ~> Object
                            int code = this.GetHashCode();  // Line  6: non-overriden virtual method call on struct
                            bool res = this.Equals(obj);    // Line  7: non-overriden virtual method call on struct
                            string caseA = E.A.ToString();  // Line  8: the same, virtual call
                            IComparable comparable = E.A;   // Line  9: valuetype conversion to interface type
                            Action<string> action = this.M; // Line 10: delegate from value type method
                            Type type = this.GetType();     // Line 11: GetType() call is always virtual
                        }

                        enum E { A, B, C }
                  }";
            var results = RunTest(script, codeLocation: "System.Void Script/Boxing::M(System.String)"); //, saveDLLForDebugging: true);
            for (int i = 4; i <= 11; i++)
            {
                AssertEx.ResultsContainAllocationType(results, AllocationType.Boxing, expectedLineNumber: i);
            }
         }

        [Fact]
        public void All_hidden_allocation_scenarios()
        {
            var script =
                @"using System;
                  using System.Collections.Generic;
                  using System.Linq;

                  class HeapAllocations {
                    List<int> _xs = new List<int>();    // explicit object creation expressions
                    int[] _ys = {1, 2, 3};              // allocation via array initializer syntax

                    void M(params string[] args) {
                        string c = args[0] + ""/"";     // Line 10: string concatenation
                        M(""abc"", ""def"");            // Line 11: parameters array allocation
                        M();                            // Line 12: the same, hidden 'new string[0]'
                        var xs = Enumerable.Range(0,1); // Line 13: iterator method call
                        var ys = from x in xs
                                    let y = x + 1       // Line 15: anonymous type creation for 'let'
                                    select x + y;
                    }

                    void N(List<string> xs) {
                        foreach (var s in xs) F(s);     // no allocations, value type enumerator

                        IEnumerable<string> ys = xs;
                        foreach (var s in ys) F(s);     // IEnumerator allocation in foreach
                    }

                    void F(string str) {
                    }
            }";
            var results = RunTest(script, codeLocation: "System.Void Script/HeapAllocations::M(System.String[])"); //, saveDLLForDebugging: true);

            // TODO Check these against the tool running in VS (see TestingResharperMemoryPlugin.cs), seems like it doesn't flag Enumerable.Range(..) on line 13?!?

            // TODO This test currently FAILS
            AssertEx.ResultsContainAllocationType(results, AllocationType.New, expectedLineNumber: 10); // Need a special case to detect String.Concat(..)

            AssertEx.ResultsContainAllocationType(results, AllocationType.New, expectedLineNumber: 11);

            AssertEx.ResultsContainAllocationType(results, AllocationType.New, expectedLineNumber: 12);

            // TODO This test currently FAILS
            AssertEx.ResultsContainAllocationType(results, AllocationType.New, expectedLineNumber: 13); // Do we need a special case to detect Enumerable.Range allocation?

            // TODO This test currently FAILS
            AssertEx.ResultsContainAllocationType(results, AllocationType.New, expectedLineNumber: 15); // Not 14 or 16, it's a multi-line statement!!
        }
    }
}
