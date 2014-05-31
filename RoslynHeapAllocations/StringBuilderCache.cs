using System;
using System.Text;

namespace RoslynHeapAllocations
{
    internal static class StringBuilderCache
    {
        [ThreadStatic]
        private static StringBuilder cachedStringBuilder;

        internal static StringBuilder AcquireBuilder()
        {
            StringBuilder result = cachedStringBuilder;
            if (result == null)
            {
                return new StringBuilder();
            }
            result.Clear();
            cachedStringBuilder = null;
            return result;
        }

        internal static string GetStringAndReleaseBuilder(StringBuilder sb)
        {
            string result = sb.ToString();
            cachedStringBuilder = sb;
            return result;
        }

    }
}
