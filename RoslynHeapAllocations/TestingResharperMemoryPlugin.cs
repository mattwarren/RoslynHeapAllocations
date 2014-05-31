using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace RoslynHeapAllocations
{
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

            // We are NOT overriding GetHashCode() here, it's just a regular method
            //public new int GetHashCode()
            //{
            //    return Test.GetHashCode();
            //}
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

        public void TestingResharperMemoryAllocationPlugin()
        {
            var test = string.Format("blah {0}", 5, 'b', "d"); // Boxing on 5 and 'b' (Resharper plugin)

            var temp = 4;
            int[] intData = new[] { 123, 32, 4 };
            IList<int> iListData = new[] { 123, 32, 4 };
            List<int> listData = new[] { 123, 32, 4 }.ToList();
            var result1 = intData.Where(x => x > temp).Count();
            var result2 = intData.Where(x => x > 5)
                                  .OrderBy(x => -x)
                                  .ToList().Count();

            var structAllocation = new DateTime(); // NO allocations (Resharper plugin)
            var classAllocation = new string('b', 3); // Allocations (Resharper plugin)

            var withBoxing = 5.ToString() + ':' + 8.ToString(); // Boxing on ':' (Resharper plugin)
            var withoutBoxing = 5.ToString() + ":" + 8.ToString();

            Color colour = Color.Red;
            var enumBoxing = colour.GetHashCode(); // Boxing (Resharper plugin)
            var fixedEnumBoxing = ((int)colour).GetHashCode();
            var noBoxing = "test".GetHashCode(); // NO boxing (Resharper plugin)

            var noGetHashCode = new NoGetHashCode(5);
            var noGetHashCodeHC = noGetHashCode.GetHashCode(); // Boxing (ResharperPlugin) because GetHashCode is NOT overridden

            var withGetHashCode = new WithGetHashCode(5);
            var withGetHashCodeHC = withGetHashCode.GetHashCode(); // NO Boxing (ResharperPlugin) because GetHashCode IS overridden
            var withGetHashCodeHCNormal = withGetHashCode.GetHashCodeNormal(); 

            int number = 5;

            // CompareTo is found on the IComparable interface
            IComparable comparable = number; // box number - boxing (Resharper plugin)
            int result3 = comparable.CompareTo(8); // box 8 - Boxing (Resharper plugin)

            // CompareTo is also a public method on int directly. In fact two overloads of CompareTo are available
            // on int, one that takes System.Object and another that takes an int, so the C# compiler will choose
            // to call the one that takes int, avoiding both boxes that the above example causes.
            int result4 = number.CompareTo(8); // NO boxing (Resharper plugin)

            M(); // boxing at call-site"

            foreach (var i in intData)
            {
            }

            foreach (var i in listData)
            {
            }

            foreach (var i in iListData) // Allocations (Resharper plugin)
            {
            }

            foreach (var i in (IEnumerable<int>)intData) // Allocations (Resharper plugin)
            {
            }
        }
    }
}
