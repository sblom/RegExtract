using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Reflection;

namespace RegExtract
{
    internal static class MatchBinder
    {
        internal static T? Extract<T>(IEnumerable<(Group group, string? name)> groups, RegExtractOptions options = RegExtractOptions.None)
        {

            T result = default;

            bool hasNamedCaptures = false;
            int numUnnamedCaptures = groups.Count() - 1;

            if (groups.All(g => g.name != null))
            {
                hasNamedCaptures = 0 < groups.Where(g  => !int.TryParse(g.name, out var _)).Count();
                numUnnamedCaptures = groups.Select((g, i) => (g, i)).Last(x => int.TryParse(x.g.name, out var n) && n == x.i).i;
            }

            var type = typeof(T);
            var constructors = type.GetConstructors()
                                   .Where(cons => cons.GetParameters().Length != 0);

            if (!hasNamedCaptures && numUnnamedCaptures == 0)
            {
                return (T)GroupToType(groups.First().group, type);
            }

            // Try to find an appropriate constructor if we have unnamed captures.
            if (numUnnamedCaptures > 0)
            {
                if (type.FullName.StartsWith("System.ValueTuple`"))
                {
                    var typeArgs = type.GetGenericArguments();

                    result = (T)CreateGenericTuple(type, groups.AsEnumerable().Select(g => g.group).Skip(1).Take(numUnnamedCaptures));
                }
                else if (constructors?.Count() == 1)
                {
                    var constructor = constructors.Single();

                    var paramTypes = constructor.GetParameters().Select(x => x.ParameterType);

                    if (paramTypes.Count() != groups.Count() - 1)
                        throw new ArgumentException($"Number of capture groups doesn't match constructor arity.");

                    result = (T)constructor.Invoke(groups.AsEnumerable().Select(g => g.group).Skip(1).Take(numUnnamedCaptures).Zip(paramTypes, GroupToType).ToArray());
                }
                else if (numUnnamedCaptures == 1 && !hasNamedCaptures)
                {
                    return (T)GroupToType(groups.Skip(1).First().group, type);
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

                    var defaultConstructor = type.GetConstructors()
                                                 .Where(cons => cons.GetParameters().Length == 0)
                                                 .SingleOrDefault();
                    if (defaultConstructor is null)
                    {
                        throw new ArgumentException("When using named capture groups, extraction type T must have a public default constructor and a public (possibly init-only) setter for each capture name. Record types work well for this.");
                    }
                    result = (T)defaultConstructor.Invoke(null);
                }

                foreach (var group in groups.Where(g => !int.TryParse(g.name, out var _) /* && g.Success */))
                {
                    var property = type.GetProperty(group.name);

                    if (property is null)
                        throw new ArgumentException($"Could not find property for named capture group '{group}'.");

                    property.GetSetMethod().Invoke(result, new object?[] { GroupToType(group.group, property.PropertyType) });
                }
            }

            if (result is null) throw new InvalidOperationException($"Couldn't find a way to convert to {type}.");

            return result;
        }

        internal static ExtractionPlan CreateExtractionPlan(IEnumerable<Group> groups, IEnumerable<string> groupNames, IEnumerable<Type> types)
        {
            return CreateExtractionPlanInner(
                new ReadOnlySlice<(Group, string)>(groups.Zip(groupNames, (group, name) => (group, name)).ToArray()), types);
        }

        internal static ExtractionPlan CreateExtractionPlanInner(ReadOnlySlice<(Group group, string name)> groups, IEnumerable<Type> types)
        {
            return new ExtractionPlan(types.First(), groups.First().group, new ExtractionPlan?[0]);
        }

        internal record ExtractionPlan(Type type, Group group, ExtractionPlan?[] items);

        internal static object? GroupToType(Group group, Type type)
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
                return Enum.Parse(type, val);
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
                return constructor.Invoke(
                    groups.Take(7)
                          .Zip(typeArgs, GroupToType)
                          .Concat(new[] { CreateGenericTuple(typeArgs.Skip(7).Single(), groups.Skip(7)) })
                          .ToArray());
            }
        }
    }
}
