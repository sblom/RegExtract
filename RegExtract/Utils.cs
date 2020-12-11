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
        internal const string VALUETUPLE_TYPENAME = "System.ValueTuple`";
        internal const string LIST_TYPENAME = "System.Collections.Generic.List`";
        internal const string NULLABLE_TYPENAME = "System.Nullable`";

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

        internal static Type[] GetGenericArgumentsFlat(Type type)
        {
            var typeArgs = type.GetGenericArguments();

            if (type.FullName.StartsWith(VALUETUPLE_TYPENAME) && typeArgs.Length == 8)
            {
                return typeArgs.Take(7).Concat(GetGenericArgumentsFlat(typeArgs[7])).ToArray();
            }
            else
            {
                return typeArgs;
            }
        }
    }
}

// This is here to enable use of record types in .NET 3.1.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
