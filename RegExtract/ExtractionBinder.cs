using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Diagnostics;

namespace RegExtract
{
    internal static class ExtractionBinder

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

        internal static int ArityOfType(Type type)
        {
            ConstructorInfo[] constructors;
            if (type.FullName.StartsWith("System.Collections.Generic.List`") || type.FullName.StartsWith("System.Nullable`"))
            {
                return ArityOfType(type.GetGenericArguments().Single());
            }
            else if (type.FullName.StartsWith("System.ValueTuple`"))
            {
                return 1 + Utils.GetGenericArgumentsFlat(type).Length;
            }
            else
            {
                constructors = type.GetConstructors().Where(cons => cons.GetParameters().Length != 0).ToArray();
                if (constructors.Length == 1)
                {
                    return 1 + constructors[0].GetParameters().Length;
                }
                else
                {
                    return 1;
                }
            }
        }

        internal static ExtractionPlan CreateExtractionPlan(IEnumerable<Group> groups, IEnumerable<string> groupNames, Type type)
        {
            // Handle the special case of a unary type at the top level.
            if (ArityOfType(type) == 1)
            {
                if (groups.Count() == 2)
                {
                    return new ExtractionPlan(type, groups.Skip(1).First(), groupNames.Skip(1).First(), new ExtractionPlan[0]);
                }
                else if (groups.Count() == 1)
                {
                    return new ExtractionPlan(type, groups.First(), groupNames.First(), new ExtractionPlan[0]);
                }
            }

            return CreateExtractionPlan(
                new ReadOnlySlice<(Group, string)>(groups.Zip(groupNames, (group, name) => (group, name)).ToArray()), type);
        }

        internal static ExtractionPlan CreateExtractionPlan(ReadOnlySlice<(Group group, string name)> groups, Type type, bool parentIsList = false)
        {
            int arity = ArityOfType(type);
            bool isList = type.FullName.StartsWith("System.Collections.Generic.List`");
            bool isNullable = type.FullName.StartsWith("System.Nullable`");
            bool isTuple = type.FullName.StartsWith("System.ValueTuple`");

            Debug.Assert(groups[0].group.Index + groups[0].group.Length >= groups[arity - 1].group.Index + groups[arity - 1].group.Length);

            if (arity == 1)
            {
                return new ExtractionPlan(type, groups[0].group, groups[0].name, new ExtractionPlan[0]);
            }

            List<(Type type, (int start, int length))> slots = new();

            int start = 1;

            Type[] typeArgs = null;

            IEnumerable<ConstructorInfo> constructors;

            if (isTuple)
            {
                typeArgs = Utils.GetGenericArgumentsFlat(type);
            }
            else if (((constructors = type.GetConstructors().Where(cons => cons.GetParameters().Length != 0)) != null) && constructors.Count() == 1)
            {
                typeArgs = constructors.Single().GetParameters().Select(p => p.ParameterType).ToArray();
            }

            foreach (Type slotType in typeArgs)
            {
                var slotArity = ArityOfType(slotType);
                slots.Add((slotType,(start, slotArity)));
                start += slotArity;
                if (start > groups.Count()) break;
            }

            return new ExtractionPlan(type, groups[0].group, groups[0].name, slots.Select(slot => CreateExtractionPlan(new ReadOnlySlice<(Group group, string name)>(groups, slot.Item2.start, slot.Item2.length), slot.type)).ToArray());
        }

        internal record ExtractionPlan(Type type, Group group, string? name, ExtractionPlan[] items)
        {
            internal object? Execute()
            {
                var constructors = type.GetConstructors()
                       .Where(cons => cons.GetParameters().Length != 0);

                if (type.FullName.StartsWith("System.ValueTuple`"))
                {
                    var typeArgs = type.GetGenericArguments();

                    return CreateGenericTuple(type, items.Select(i => i.Execute()));
                }
                else if (constructors?.Count() == 1)
                {
                    var constructor = constructors.Single();

                    var paramTypes = constructor.GetParameters().Select(x => x.ParameterType);

                    if (paramTypes.Count() != items.Count())
                        throw new ArgumentException($"Number of capture groups doesn't match constructor arity.");

                    return constructor.Invoke(items.Select(i => i.Execute()).ToArray());
                }
                else
                {
                    return GroupToType(group, type);
                }
            }
        };

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

        internal static object CreateGenericTuple(Type tupleType, IEnumerable<object> vals)
        {
            var typeArgs = tupleType.GetGenericArguments();
            var constructor = tupleType.GetConstructor(tupleType.GetGenericArguments());

            if (typeArgs.Count() <= 7)
            {
                return constructor.Invoke(vals.ToArray());
            }
            else
            {
                return constructor.Invoke(vals.Take(7)
                          .Concat(new[] { CreateGenericTuple(typeArgs[7], vals.Skip(7)) })
                          .ToArray());
            }

        }

        internal static object CreateGenericTuple(Type tupleType, IEnumerable<Group> groups)
        {
            var typeArgs = Utils.GetGenericArgumentsFlat(tupleType);

            if (groups.Count() != typeArgs.Count())
                throw new ArgumentException($"Number of capture groups doesn't match tuple arity.");

            return CreateGenericTuple(tupleType, groups.Zip(typeArgs, GroupToType));
        }
    }
}
