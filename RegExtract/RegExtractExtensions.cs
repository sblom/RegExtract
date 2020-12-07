using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace RegExtract
{
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

        private static object StringToType(string val, Type type)
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

            return val;
        }

        private static object CreateGenericTuple(Type tupleType, IEnumerable<string> values)
        {
            var typeArgs = (IEnumerable<Type>)tupleType.GenericTypeArguments;
            var constructor = tupleType.GetConstructor(tupleType.GenericTypeArguments);

            if (typeArgs.Count() <= 7)
            {
                if (values.Count() != typeArgs.Count())
                    throw new ArgumentException($"Number of capture groups doesn't match tuple arity.");

                return constructor.Invoke(values.Zip(typeArgs, StringToType).ToArray());
            }
            else
            {
                return constructor.Invoke(values.Take(7).Zip(typeArgs, StringToType).Append(CreateGenericTuple(typeArgs.Skip(7).Single(), values.Skip(7))).ToArray());
            }
        }

        public static T Extract<T>(this Match match)
        {
            if (!match.Success)
                throw new ArgumentException("Regex failed to match input.");

            var type = typeof(T);
            var constructors = type.GetConstructors().Where(cons => cons.GetParameters().Length != 0);

#if !NETSTANDARD2_0
            bool hasNamedCaptures = 0 < match.Groups.AsEnumerable().Where(g => g.Name is { Length: >0 } && !int.TryParse(g.Name, out var _)).Count();
            if (hasNamedCaptures)
            {
                var defaultConstructor = type.GetConstructors().Where(cons => cons.GetParameters().Length == 0).SingleOrDefault();
                if (defaultConstructor is null)
                {
                    throw new ArgumentException("When using named capture groups, extraction type T must have a public default constructor and a public (possibly init-only) setter for each capture name. Record types work well for this.");
                }

                var result = (T)defaultConstructor.Invoke(null);

                foreach (var group in match.Groups.AsEnumerable().Where(g => !int.TryParse(g.Name, out var _) /* && g.Success */))
                {
                    var property = type.GetProperty(group.Name);

                    if (property is null)
                        throw new ArgumentException($"Could not find property for named capture group '{group.Name}'.");

                    if (property.PropertyType.FullName.StartsWith("System.Collections.Generic.List`"))
                    {
                        var listType = property.PropertyType.GenericTypeArguments.Single();
                        var list = group.Captures.AsEnumerable().Select(c => StringToType(c.Value,listType));

                        MethodInfo CastMethod = typeof(Enumerable).GetMethod("Cast");
                        MethodInfo ToListMethod = typeof(Enumerable).GetMethod("ToList");

                        var castItems = CastMethod.MakeGenericMethod(new Type[] { listType })
                                                  .Invoke(null, new object[] { list });
                        var listout = ToListMethod.MakeGenericMethod(new Type[] { listType })
                                                  .Invoke(null, new object[] { castItems });

                        property.GetSetMethod().Invoke(result, new object[] {listout});
                    }
                    else
                    {
                        if (!group.Success) continue;
                        property.GetSetMethod().Invoke(result, new object[] {StringToType(group.Value,property.PropertyType)});
                    }
                }

                return result;
            }
            else
#endif
            if (type.FullName.StartsWith("System.ValueTuple`"))
            {
                var typeArgs = type.GenericTypeArguments;

                return (T)(CreateGenericTuple(type, match.Groups.AsEnumerable().Select(g => g.Value).Skip(1)));
            }
            else if (constructors?.Count() == 1)
            {
                var constructor = constructors.Single();

                var paramTypes = constructor.GetParameters().Select(x => x.ParameterType);

                if (paramTypes.Count() != match.Groups.Count - 1)
                    throw new ArgumentException($"Number of capture groups doesn't match constructor arity.");

                return (T)constructor.Invoke(match.Groups.AsEnumerable().Select(g => g.Value).Skip(1).Zip(paramTypes, StringToType).ToArray());
            }
            else
            {
                throw new ArgumentException("When not using named captures, your extraction type T must be either a ValueTuple or a type with a single public non-default constructor, such as a record.");
            }
        }

        public static T Extract<T>(this string str, string rx)
        {
            var match = Regex.Match(str, rx);

            return (T)(Extract<T>(match));
        }
    }
}
