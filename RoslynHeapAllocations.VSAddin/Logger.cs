using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoslynHeapAllocations.VSAddin
{
    internal static class Logger
    {
        internal static void Log(string format, params object[] args)
        {
            var text = String.Format(format, args);
            Trace.WriteLine(text);
            //Debug.WriteLine(text);
        }

        internal static void Log(object @object)
        {
            Trace.WriteLine(@object);
        }

        internal static void Log(string text)
        {
            Trace.WriteLine(text);
            //Debug.WriteLine(text);
        }

        internal static void Log()
        {
            Trace.WriteLine(String.Empty);
            //Debug.WriteLine(String.Empty);
        }

        internal static void LogWithoutNewLine(string text)
        {
            Trace.Write(text);
            //Debug.Write(text);
        }
    }
}
