using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace RegExtract
{
    internal static class Utils
    {
        internal static IEnumerable<Group> AsEnumerable(this GroupCollection gc)
        {
            foreach (Group g in gc)
            {
                yield return g;
            }
        }

        internal static IEnumerable<Capture> AsEnumerable(this CaptureCollection cc)
        {
            foreach (Capture c in cc)
            {
                yield return c;
            }
        }

    }
}

// This is here to enable use of record types in .NET 3.1.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}