using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace RegExtract
{
    public record RootVirtualTupleExtractionPlanNode(string groupName, Type type, ExtractionPlanNode[] constructorNodes, ExtractionPlanNode[] propertyNodes): 
        ExtractionPlanNode(groupName, type, constructorNodes, propertyNodes)
    {
        internal override object? Execute(Match match, int captureStart, int captureLength)
        {
            return constructorNodes.Single().Execute(match, captureStart, captureLength);
        }
    }

    public record ExtractionPlanNode(string groupName, Type type, ExtractionPlanNode[] constructorNodes, ExtractionPlanNode[] propertyNodes)
    {
        public object? Execute(Match match)
        {
            return Execute(match, match.Groups[groupName].Index, match.Groups[groupName].Length);
        }

        internal virtual object? Execute(Match match, int captureStart, int captureLength)
        {
            object? result = null;

            var ranges = AsEnumerable(match.Groups[groupName].Captures)
                  .Where(cap => cap.Index >= captureStart && cap.Index + cap.Length <= captureStart + captureLength)
                  .Select(cap => (cap.Value, cap.Index, cap.Length));

            Type innerType = IsNullable(type) ? type.GetGenericArguments().Single() : type;

            bool isList = IsList(type);

            if (!isList)
            {
                if (!ranges.Any())
                {
                    if (type.IsClass || Nullable.GetUnderlyingType(type) != null) return null;
                    else return Convert.ChangeType(null, type);
                }
                else
                {
                    var lastRange = ranges.Last();
                    result = RangeToType(match, innerType, lastRange);
                }
            }
            else
            {
                var listType = type.GetGenericArguments().Single();

                //var elementType = IsList(listType) ? listType.GetGenericArguments().Single() : listType;

                var vals = ranges.Select(range => RangeToType(match, listType, range));

                MethodInfo CastMethod = typeof(Enumerable).GetMethod("Cast");
                MethodInfo ToListMethod = typeof(Enumerable).GetMethod("ToList");

                var castItems = CastMethod.MakeGenericMethod(new Type[] { listType })
                                            .Invoke(null, new object[] { vals });
                var listout = ToListMethod.MakeGenericMethod(new Type[] { listType })
                                            .Invoke(null, new object[] { castItems });

                result = listout;

            }

            foreach (var prop in propertyNodes)
            {
                var lastRange = ranges.Last();
                result.GetType().GetProperty(prop.groupName).GetSetMethod().Invoke(result, new[] { prop.Execute(match, lastRange.Index, lastRange.Length) });
            }

            return result;
        }

        object? RangeToType(Match match, Type type, (string Value, int Index, int Length) range)
        {
            object? result;
            var constructors = type.GetConstructors()
                .Where(cons => cons.GetParameters().Length == constructorNodes.Length);

            if (IsNullable(type)) type = type.GetGenericArguments().Single();

            if (type.FullName.StartsWith(VALUETUPLE_TYPENAME))
            {
                result = CreateGenericTuple(type, constructorNodes.Select(i => i.Execute(match, range.Index, range.Length)));
            }
            else if (constructors?.Count() == 1)
            {
                var constructor = constructors.Single();

                var paramTypes = constructor.GetParameters().Select(x => x.ParameterType);

                if (paramTypes.Count() != constructorNodes.Length)
                    throw new ArgumentException($"Number of capture groups doesn't match constructor arity.");

                result = constructor.Invoke(constructorNodes.Select(i => i.Execute(match, range.Index, range.Length)).ToArray());
            }
            else
            {
                result = StringToType(range.Value, type);
            }

            return result;
        }

        protected object StringToType(string val, Type type)
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

        protected object CreateGenericTuple(Type tupleType, IEnumerable<object?> vals)
        {
            var typeArgs = tupleType.GetGenericArguments();
            var constructor = tupleType.GetConstructor(tupleType.GetGenericArguments());

            if (vals.Count() != typeArgs.Count())
                throw new ArgumentException($"Number of capture groups doesn't match tuple arity.");

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



        protected const string VALUETUPLE_TYPENAME = "System.ValueTuple`";
        protected const string LIST_TYPENAME = "System.Collections.Generic.List`";
        protected const string NULLABLE_TYPENAME = "System.Nullable`";

        protected bool IsList(Type type)
        {
            return type.FullName.StartsWith(LIST_TYPENAME);
        }

        protected bool IsTuple(Type type)
        {
            return type.FullName.StartsWith(VALUETUPLE_TYPENAME);
        }

        protected bool IsNullable(Type type)
        {
            return type.FullName.StartsWith(NULLABLE_TYPENAME);
        }

        private IEnumerable<Capture> AsEnumerable(CaptureCollection cc)
        {
            foreach (Capture c in cc)
            {
                yield return c;
            }
        }
    }
}
