using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace RegExtract
{
    public static class RegExtractExtensions
    {
        public static IEnumerable<Group> AsEnumerable(this GroupCollection gc)
        {
            foreach (Group g in gc)
            {
                yield return g;
            }
        }

        private static object StringToType(string val, Type type)
        {
            var parse = type.GetMethod("Parse",
                                        BindingFlags.Static | BindingFlags.Public,
                                        null,
                                        new Type[] { typeof(string) },
                                        null);

            if (parse is not null)
            {
                return parse.Invoke(null, new object[] { val });
            }

            var constructor = type.GetConstructor(new[] { typeof(string) });

            if (constructor is not null)
            {
                return constructor.Invoke(new[] { val });
            }

            return val;
        }

        private static object CreateGenericTuple(Type tupleType, IEnumerable<string> values)
        {
            var typeArgs = (IEnumerable<Type>)tupleType.GenericTypeArguments;
            var constructor = tupleType.GetConstructor(tupleType.GenericTypeArguments);

            if (typeArgs.Count() <= 7)
            {
                if (values.Count() != typeArgs.Count())
                    throw new ArgumentException($"Length of {nameof(values)} doesn't match tuple type.");

                return constructor.Invoke(values.Zip(typeArgs, StringToType).ToArray());
            }
            else
            {
                return constructor.Invoke(values.Take(7).Zip(typeArgs, StringToType).Append(CreateGenericTuple(typeArgs.Skip(7).Single(), values.Skip(7))).ToArray());
            }
        }

        public static T Extract<T>(this Match match)
        {
            var type = typeof(T);
            var typeArgs = type.GenericTypeArguments;

            return (T)(CreateGenericTuple(type, match.Groups.AsEnumerable().Select(g => g.Value).Skip(1)));
        }

        public static T Extract<T>(this string str, string rx)
        {
            var match = Regex.Match(str, rx);

            return (T)(Extract<T>(match));
        }
    }
}
