using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace RegExtract
{
    public record VirtualUnaryTupleNode(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters):
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        internal override object? Execute(Match match, int captureStart, int captureLength)
        {
            return constructorParams.Single().Execute(match, captureStart, captureLength);
        }

        internal override void Validate()
        {
            base.Validate();
        }
    }

    public record ListOfListsNode(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        internal override object? Construct(Match match, Type type, (string Value, int Index, int Length) range)
        {
            return constructorParams.Single().Execute(match, range.Index, range.Length);
        }

        internal override void Validate()
        {
            if (!IsList(type) || !IsList(type.GetGenericArguments().Single()))
                throw new InvalidOperationException($"{nameof(ListOfListsNode)} assigned type other than List<List<T>>");

            base.Validate();
        }
    }

    public record TupleNode(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        internal override object? Construct(Match match, Type type, (string Value, int Index, int Length) range)
        {
            type = IsNullable(type) ? type.GetGenericArguments().Single() : type;

            return CreateGenericTuple(type, constructorParams.Select(i => i.Execute(match, range.Index, range.Length)));
        }

        internal override void Validate()
        {
            var unwrappedType = IsList(type) ? type.GetGenericArguments().Single() : type;
            unwrappedType = IsNullable(unwrappedType) ? unwrappedType.GetGenericArguments().Single() : unwrappedType;

            if (!IsTuple(unwrappedType))
                throw new InvalidOperationException($"{nameof(ListOfListsNode)} assigned type other than ValueTuple<>");

            base.Validate();
        }
    }

    public record ConstructorWithParamsNode(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        internal override object? Construct(Match match, Type type, (string Value, int Index, int Length) range)
        {
            type = IsNullable(type) ? type.GetGenericArguments().Single() : type;

            var constructors = type.GetConstructors()
                .Where(cons => cons.GetParameters().Length == constructorParams.Length);

            var constructor = constructors.Single();

            var paramTypes = constructor.GetParameters().Select(x => x.ParameterType);

            return constructor.Invoke(constructorParams.Select(i => i.Execute(match, range.Index, range.Length)).ToArray());
        }

        internal override void Validate()
        {
            var unwrappedType = IsNullable(type) ? type.GetGenericArguments().Single() : type;

            var constructors = unwrappedType.GetConstructors()
                .Where(cons => cons.GetParameters().Length == constructorParams.Length);

            if (constructors.Count() != 1)
                throw new InvalidOperationException($"{nameof(ConstructorWithParamsNode)} has wrong number of constructor params.");

            base.Validate();
        }
    }

    public record EnumNode(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        internal override object? Construct(Match match, Type type, (string Value, int Index, int Length) range)
        {
            return Enum.Parse(type, range.Value);
        }
    }

    public record StringConstructorNode(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        internal override object? Construct(Match match, Type type, (string Value, int Index, int Length) range)
        {
            type = IsNullable(type) ? type.GetGenericArguments().Single() : type;

            var constructor = type.GetConstructor(new[] { typeof(string) });
        
            return constructor.Invoke(new[] { range.Value });
        }

        internal override void Validate()
        {
            var unwrappedType = IsNullable(type) ? type.GetGenericArguments().Single() : type;

            var constructor = unwrappedType.GetConstructor(new[] { typeof(string) });

            if (constructor is null || constructorParams.Length != 0)
                throw new InvalidOperationException($"{nameof(StringConstructorNode)} has wrong type or constructor params.");

            base.Validate();
        }
    }

    public record StaticParseNode(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        internal override object? Construct(Match match, Type type, (string Value, int Index, int Length) range)
        {
            type = IsList(type) ? type.GetGenericArguments().Single() : type; 
            type = IsNullable(type) ? type.GetGenericArguments().Single() : type;

            var parse = type.GetMethod("Parse",
                            BindingFlags.Static | BindingFlags.Public,
                            null,
                            new Type[] { typeof(string) },
                            null);

                return parse.Invoke(null, new object[] { range.Value });
            }

        internal override void Validate()
        {
            var unwrappedType = IsList(type) ? type.GetGenericArguments().Single() : type;
            unwrappedType = IsNullable(unwrappedType) ? unwrappedType.GetGenericArguments().Single() : unwrappedType;

            var parse = unwrappedType.GetMethod("Parse",
                            BindingFlags.Static | BindingFlags.Public,
                            null,
                            new Type[] { typeof(string) },
                            null);

            if (parse is null)
                throw new InvalidOperationException($"{nameof(StaticParseNode)} has wrong type or constructor params.");

            base.Validate();
        }
    }

    public record StringNode(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        internal override object? Construct(Match match, Type type, (string Value, int Index, int Length) range)
        {
            return range.Value;
        }
    }

    public record ExtractionPlanNode(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertyNodes)
    {
        internal static ExtractionPlanNode Bind(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters)
        {
            var innerType = IsList(type) ? type.GetGenericArguments().Single() : type;
            innerType = IsNullable(innerType) ? innerType.GetGenericArguments().Single() : innerType;

            var multiConstructor = innerType.GetConstructors()
                .Where(cons => cons.GetParameters().Length == constructorParams.Length);

            var parse = innerType.GetMethod("Parse",
                                        BindingFlags.Static | BindingFlags.Public,
                                        null,
                                        new Type[] { typeof(string) },
                                        null);

            var constructor = innerType.GetConstructor(new[] { typeof(string) });



            ExtractionPlanNode node;

            if (IsList(innerType))
                node = new ListOfListsNode(groupName, type, constructorParams, propertySetters);
            else if (IsTuple(innerType))
                node = new TupleNode(groupName, type, constructorParams, propertySetters);
            else if (multiConstructor.Count() == 1)
                node = new ConstructorWithParamsNode(groupName, type, constructorParams, propertySetters);
            else if (parse is not null)
                node = new StaticParseNode(groupName, type, constructorParams, propertySetters);
            else if (constructor is not null)
                node = new StringConstructorNode(groupName, type, constructorParams, propertySetters);
            else if (innerType.BaseType == typeof(Enum))
                node = new EnumNode(groupName, type, constructorParams, propertySetters);
            else
                node = new StringNode(groupName, type, constructorParams, propertySetters);

            node.Validate();

            return node;
        }

        internal virtual void Validate()
        {
            return;
        }

        internal virtual object? Construct(Match match, Type type, (string Value, int Index, int Length) range)
        {
            throw new InvalidOperationException();
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

                    result = Construct(match, innerType, lastRange);

                    foreach (var prop in propertyNodes)
                    {
                        result.GetType().GetProperty(prop.groupName).GetSetMethod().Invoke(result, new[] { prop.Execute(match, lastRange.Index, lastRange.Length) });
                    }
                }
            }
            else
            {
                var listType = type.GetGenericArguments().Single();

                //var elementType = IsList(listType) ? listType.GetGenericArguments().Single() : listType;

                List<object?> vals = new();
                
                foreach (var range in ranges)
                {
                    var itemVal = Construct(match, listType, range);

                    foreach (var prop in propertyNodes)
                    {
                        itemVal.GetType().GetProperty(prop.groupName).GetSetMethod().Invoke(result, new[] { prop.Execute(match, range.Index, range.Length) });
                    }

                    vals.Add(itemVal);
                }

                MethodInfo CastMethod = typeof(Enumerable).GetMethod("Cast");
                MethodInfo ToListMethod = typeof(Enumerable).GetMethod("ToList");

                var castItems = CastMethod.MakeGenericMethod(new Type[] { listType })
                                            .Invoke(null, new object[] { vals });
                var listout = ToListMethod.MakeGenericMethod(new Type[] { listType })
                                            .Invoke(null, new object[] { castItems });

                result = listout;
            }

            return result;
        }

        public object? Execute(Match match)
        {
            if (!match.Success)
            {
                throw new ArgumentException("Regex didn't match.");
            }

            return Execute(match, match.Groups[groupName].Index, match.Groups[groupName].Length);
        }

        protected object CreateGenericTuple(Type tupleType, IEnumerable<object?> vals)
        {
            var typeArgs = tupleType.GetGenericArguments();
            var constructor = tupleType.GetConstructor(tupleType.GetGenericArguments());

            if (typeArgs.Count() < 8 && vals.Count() != typeArgs.Count())
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

        protected static bool IsList(Type type)
        {
            return type.FullName.StartsWith(LIST_TYPENAME);
        }

        protected static bool IsTuple(Type type)
        {
            return type.FullName.StartsWith(VALUETUPLE_TYPENAME);
        }

        protected static bool IsNullable(Type type)
        {
            return type.FullName.StartsWith(NULLABLE_TYPENAME);
        }

        protected static IEnumerable<Capture> AsEnumerable(CaptureCollection cc)
        {
            foreach (Capture c in cc)
            {
                yield return c;
            }
        }
    }
}
