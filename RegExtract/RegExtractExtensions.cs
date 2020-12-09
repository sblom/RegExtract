﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace RegExtract
{
    public enum RegExtractOptions
    {
        None = 0x0,
        Strict = 0x1
    }

    public static class RegExtractExtensions
    {
        private static IEnumerable<Group> AsEnumerable(this GroupCollection gc)
        {
            foreach (Group g in gc)
            {
                yield return g;
            }
        }

        private static IEnumerable<Capture> AsEnumerable(this CaptureCollection cc)
        {
            foreach (Capture c in cc)
            {
                yield return c;
            }
        }

        internal static object GroupToType(Group group, Type type)
        {
            if (type.FullName.StartsWith("System.Collections.Generic.List`"))
            {
                var listType = type.GetGenericArguments().Single();
                var list = group.Captures.AsEnumerable().Select(c => StringToType(c.Value, listType));

                MethodInfo CastMethod = typeof(Enumerable).GetMethod("Cast");
                MethodInfo ToListMethod = typeof(Enumerable).GetMethod("ToList");

                var castItems = CastMethod.MakeGenericMethod(new Type[] { listType })
                                          .Invoke(null, new object[] { list });
                var listout = ToListMethod.MakeGenericMethod(new Type[] { listType })
                                          .Invoke(null, new object[] { castItems });

                return listout;
            }
            else if (group.Success)
            {
                return StringToType(group.Value, type);
            }
            else
            {
                if (type.IsClass || Nullable.GetUnderlyingType(type) != null) return null;
                else return Convert.ChangeType(null, type);
            }
        }

        internal static object StringToType(string val, Type type)
        {
            if (type.FullName.StartsWith("System.Nullable`1"))
            {
                type = type.GetGenericArguments().Single();
            }

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

            if (type.BaseType == typeof(Enum))
            {
                return typeof(Enum).GetMethods().Where(m => m.Name == "Parse" && m.IsGenericMethod && m.GetParameters().Count() == 1)
                            .First().MakeGenericMethod(new[] { type }).Invoke(null, new[] { val });
            }

            return val;
        }

        internal static object CreateGenericTuple(Type tupleType, IEnumerable<Group> groups)
        {
            var typeArgs = (IEnumerable<Type>)tupleType.GetGenericArguments();
            var constructor = tupleType.GetConstructor(tupleType.GetGenericArguments());

            if (typeArgs.Count() <= 7)
            {
                if (groups.Count() != typeArgs.Count())
                    throw new ArgumentException($"Number of capture groups doesn't match tuple arity.");

                return constructor.Invoke(groups.Zip(typeArgs, GroupToType).ToArray());
            }
            else
            {
                return constructor.Invoke(groups.Take(7).Zip(typeArgs, GroupToType).Concat(new[] { CreateGenericTuple(typeArgs.Skip(7).Single(), groups.Skip(7)) }).ToArray());
            }
        }

        public static T Extract<T>(this Match match, RegExtractOptions options = RegExtractOptions.None, string[] groupNames = null)
        {
            if (!match.Success)
                throw new ArgumentException("Regex failed to match input.");

            T result = default;

            bool hasNamedCaptures = false;
            int numUnnamedCaptures = match.Groups.Count - 1;

            if (groupNames == null)
            {
#if !NETSTANDARD2_0 && !NET40
                groupNames = match.Groups.Select(g => g.Name).ToArray();
#endif
            }

            if (groupNames != null)
            {
                hasNamedCaptures = groupNames.Where(gn => !int.TryParse(gn, out var _)).Any();
                numUnnamedCaptures = groupNames.Select((gn,i) => (gn, i)).Last(x => int.TryParse(x.gn, out var n) && n == x.i).i;
            }

            var type = typeof(T);
            var constructors = type.GetConstructors().Where(cons => cons.GetParameters().Length != 0);

            if (!hasNamedCaptures && numUnnamedCaptures == 0)
            {
                return (T)GroupToType(match.Groups[0], type);
            }

            // Try to find an appropriate constructor if we have unnamed captures.
            if (numUnnamedCaptures > 0)
            {
                if (type.FullName.StartsWith("System.ValueTuple`"))
                {
                    var typeArgs = type.GetGenericArguments();

                    result = (T)(CreateGenericTuple(type, match.Groups.AsEnumerable().Skip(1).Take(numUnnamedCaptures)));
                }
                else if (constructors?.Count() == 1)
                {
                    var constructor = constructors.Single();

                    var paramTypes = constructor.GetParameters().Select(x => x.ParameterType);

                    if (paramTypes.Count() != match.Groups.Count - 1)
                        throw new ArgumentException($"Number of capture groups doesn't match constructor arity.");

                    result = (T)constructor.Invoke(match.Groups.AsEnumerable().Skip(1).Take(numUnnamedCaptures).Zip(paramTypes, GroupToType).ToArray());
                }
                else if (numUnnamedCaptures == 1 && !hasNamedCaptures)
                {
                    return (T)GroupToType(match.Groups[1], type);
                }
                else if (!hasNamedCaptures)
                {
                    throw new ArgumentException("When not using named captures, your extraction type T must be either a ValueTuple or a type with a single public non-default constructor, such as a record.");
                }
            }

            if (hasNamedCaptures)
            {
                if (ReferenceEquals(result, default(T)) || Equals(result, default(T)))
                {
                    if (options.HasFlag(RegExtractOptions.Strict))
                        throw new ArgumentException("No constructor could be found that matched the number of unnamed capture groups. Because options includes Strict option we can't fall back to a default constructor and ignore unnamed captures.");

                    var defaultConstructor = type.GetConstructors().Where(cons => cons.GetParameters().Length == 0).SingleOrDefault();
                    if (defaultConstructor is null)
                    {
                        throw new ArgumentException("When using named capture groups, extraction type T must have a public default constructor and a public (possibly init-only) setter for each capture name. Record types work well for this.");
                    }
                    result = (T)defaultConstructor.Invoke(null);
                }

                foreach (var group in groupNames.Where(gn => !int.TryParse(gn, out var _) /* && g.Success */))
                {
                    var property = type.GetProperty(group);

                    if (property is null)
                        throw new ArgumentException($"Could not find property for named capture group '{group}'.");

                    property.GetSetMethod().Invoke(result, new object[] {GroupToType(match.Groups[group],property.PropertyType)});
                }
            }

            return result;
        }

        public static T Extract<T>(this string str, string rx, RegExtractOptions options = RegExtractOptions.None)
        {
            var match = Regex.Match(str, rx);

            string[] groupNames = null;
#if NETSTANDARD2_0 || NET40
             groupNames = new Regex(rx).GetGroupNames();
#endif

            return (T)Extract<T>(match, options, groupNames);
        }

        public static T Extract<T>(this string str, Regex rx, RegExtractOptions options = RegExtractOptions.None)
        {
            var match = rx.Match(str);

            string[] groupNames = null;
#if NETSTANDARD2_0 || NET40
            groupNames = rx.GetGroupNames();
#endif
            
            return (T)Extract<T>(match, options,groupNames);
        }

        public static T Extract<T>(this string str, RegExtractOptions options = RegExtractOptions.None)
        {
            var field = typeof(T).GetField("REGEXTRACT_REGEX_PATTERN", BindingFlags.Public | BindingFlags.Static);
            if (field is not { IsLiteral: true, IsInitOnly: false }) throw new ArgumentException("No string, Regex, or Match provided, and extraction type doesn't have public const string REGEXTRACT_REGEX_PATTERN.");
            string rxPattern = (string)field.GetValue(null);

            RegexOptions rxOptions = RegexOptions.None;
            field = typeof(T).GetField("REGEXTRACT_REGEX_OPTIONS", BindingFlags.Public | BindingFlags.Static);
            if (field is { IsLiteral: true, IsInitOnly: false }) rxOptions = (RegexOptions)field.GetValue(null);

            var match = Regex.Match(str, rxPattern, rxOptions);

            string[] groupNames = null;
#if NETSTANDARD2_0 || NET40
            groupNames = new Regex(rxPattern, rxOptions).GetGroupNames();
#endif

            return (T)(Extract<T>(match, options, groupNames));
        }
    }
}
