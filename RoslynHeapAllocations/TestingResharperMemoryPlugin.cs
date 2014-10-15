using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RoslynHeapAllocations
{
    /// <summary>
    /// The purpose of the class is to so that we can have some sample code to tryout the plugin with
    /// The code isn't meant to be run, it's just here so we can open this file in VS and see the results
    /// Also it can be used with the Resharper Plugin (https://resharper-plugins.jetbrains.com/packages/ReSharper.HeapView/0.9.1)
    /// to compare the results and see if we detect all the issues they do
    /// </summary>
    public class TestingResharperMemoryPlugin
    {
        public enum Color
        {
            Red, Green, Blue
        }

        public struct NoGetHashCode
        {
            public readonly int Test;

            public NoGetHashCode(int test)
            {
                Test = test;
            }

            // We are delibrately NOT overriding GetHashCode() here
        }

        public struct WithGetHashCode
        {
            public readonly int Test;

            public WithGetHashCode(int test)
            {
                Test = test;
            }

            public override int GetHashCode()
            {
                return Test.GetHashCode();
            }

            public int GetHashCodeNormal()
            {
                return Test.GetHashCode();
            }
        }

        void M([Optional, DefaultParameterValue(42)] object o)
        {
        }

        struct Boxing
        {
            void M(string a)
            {
                object obj = 42;                // implicit conversion Int32 ~> Object
                string path = a + '/' + obj;    // implicit conversion Char ~> Object
                string pathOkay = a + "/" + obj;
                int code = this.GetHashCode();  // non-overriden virtual method call on struct
                string caseA = E.A.ToString();  // the same, virtual call
                IComparable comparable = E.A;   // valuetype conversion to interface type
                Action<string> action = this.M; // delegate from value type method
                Type type = this.GetType();     // GetType() call is always virtual
            }

            enum E { A, B, C }
        }

        public void TestingResharperMemoryAllocationPlugin()
        {
            var test = string.Format("blah {0}", 5, 'b', "d"); // Boxing on 5 and 'b' 

            var temp = 4;
            int[] intData = new[] { 123, 32, 4 };
            var result1 = intData.Where(x => x > temp).Count();
            var result2 = intData.Where(x => x > 5)
                                  .OrderBy(x => -x)
                                  .ToList().Count();

            var structAllocation = new DateTime();     // NO allocations 
            var classAllocation = new string('b', 3);  // Allocations 

            var withBoxing = 5.ToString() + ':' + 8.ToString(); // Boxing on ':' 
            var withoutBoxing = 5.ToString() + ":" + 8.ToString();

            Color colour = Color.Red;
            var enumBoxing = colour.GetHashCode(); // Boxing 
            var fixedEnumBoxing = ((int)colour).GetHashCode(); // NO boxing
            var noBoxing = "test".GetHashCode(); // NO boxing 

            var noGetHashCode = new NoGetHashCode(5);
            var noGetHashCodeHC = noGetHashCode.GetHashCode(); // Boxing because GetHashCode is NOT overridden

            var withGetHashCode = new WithGetHashCode(5);
            var withGetHashCodeHC = withGetHashCode.GetHashCode(); // NO Boxing because GetHashCode IS overridden
            var withGetHashCodeHCNormal = withGetHashCode.GetHashCodeNormal(); 

            int number = 5;

            // CompareTo is found on the IComparable interface
            IComparable comparable = number; // box number - Boxing 
            int result3 = comparable.CompareTo(8); // box 8 - Boxing 

            // CompareTo is also a public method on int directly. In fact two overloads of CompareTo are available
            // on int, one that takes System.Object and another that takes an int, so the C# compiler will choose
            // to call the one that takes int, avoiding both boxes that the above example causes.
            int result4 = number.CompareTo(8); // NO boxing 

            M(); // boxing at call-site"

            foreach (var i in intData)
            {
            }

            IList<int> iListData = new[] { 123, 32, 4 };
            List<int> listData = new[] { 123, 32, 4 }.ToList();

            foreach (var i in listData)
            {
            }

            foreach (var i in iListData) // Hidden Allocations
            {
            }

            foreach (var i in (IEnumerable<int>)intData) // Hidden Allocations
            {
            }
        }

        class HeapAllocations
        {
            List<int> _xs = new List<int>();  // explicit object creation expressions
            int[] _ys = { 1, 2, 3 }; // allocation via array initializer syntax

            void M(params string[] args)
            {
                string c = args[0] + "/";        // string concatenation
                M("abc", "def");                 // parameters array allocation
                M();                             // the same, hidden 'new string[0]'
                var xs = Enumerable.Range(0, 1); // iterator method call
                var ys = from x in xs
                         let y = x + 1           // anonymous type creation for 'let'
                         select x + y;

                var ys1 = from x in xs
                          let y = x + 1           // anonymous type creation for 'let'
                          select x + y;

                var ys2 = from x in xs
                          let y = x + 1           // anonymous type creation for 'let'
                          select x + y;
            }

            void N(List<string> xs)
            {
                foreach (var s in xs) F(s);     // no allocations, value type enumerator

                IEnumerable<string> ys = xs;
                foreach (var s in ys) F(s);     // IEnumerator allocation in foreach
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            void F(string test)
            {
            }
        }
    }
}