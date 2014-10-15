using System;
using System.Text;

namespace RoslynHeapAllocations
{
    // "Borrowed" from the Roslyn code-base
    // The BCL one seems to be tailored fro a different scenarion (smaller string)
    // http://referencesource.microsoft.com/#mscorlib/system/text/stringbuildercache.cs
    public static class StringBuilderCache
    {
        [ThreadStatic]
        private static StringBuilder cachedStringBuilder;

        public static StringBuilder AcquireBuilder()
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

        public static string GetStringAndReleaseBuilder(StringBuilder sb)
        {
            string result = sb.ToString();
            cachedStringBuilder = sb;
            return result;
        }
    }
}
