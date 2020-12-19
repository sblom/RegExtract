using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using RegExtract.ExtractionPlanNodeTypes;

namespace RegExtract
{
    public record ExtractionPlanNode(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertyNodes)
    {
        public string ShowPlanTree()
        {
            StringBuilder builder = new();

            builder.Append(this.GetType().Name.Replace("Node","")).Append("<").Append(string.Join(",",FriendlyTypeName(type))).Append(">[").Append(int.TryParse(groupName, out var _) ? groupName : "\"" + groupName + "\"").Append("] (");
            if (constructorParams.Any())
            {
                builder.Append("\n");
                builder.Append(string.Join(",\n", constructorParams.Select(param => "\t" + param.ShowPlanTree().Replace("\n", "\n\t"))));
                builder.Append("\n)");
            }
            else
            {
                builder.Append(")");
            }
            if (propertyNodes.Any())
            {
                builder.Append(" {\n");
                builder.Append(string.Join(",\n", propertyNodes.Select(param => "\t" + param.groupName + " = " + param.ShowPlanTree().Replace("\n", "\n\t"))));
                builder.Append("\n}");
            }

            return builder.ToString();
        }

        string FriendlyTypeName(Type type)
        {
            var keyword = type.Name switch
            {
                "Byte" => "byte",
                "SByte" => "sbyte",
                "Float" => "float",
                "Double" => "double",
                "Decimal" => "decimal",
                "Int16" => "short",
                "UInt16" => "ushort",
                "Int32" => "int",
                "UInt32" => "uint",
                "Int64" => "long",
                "UInt64" => "ulong",
                "Char" => "char",
                "String" => "string",
                _ => null
            };

            if (keyword is not null) return keyword;

            if (IsNullable(type)) return FriendlyTypeName(type.GetGenericArguments().Single()) + "?";

            var args = type.GetGenericArguments();

            if (IsTuple(type)) return "(" + String.Join(",", args.Select(arg => FriendlyTypeName(arg))) + ")";

            if (args.Any())
            {
                return type.Name.Split('`')[0] + "<" + String.Join(",", args.Select(arg => FriendlyTypeName(arg))) + ">";
            }

            else return type.Name;
        }

        internal static ExtractionPlanNode Bind(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters)
        {
            var innerType = IsList(type) ? type.GetGenericArguments().Single() : type;
            innerType = IsNullable(innerType) ? innerType.GetGenericArguments().Single() : innerType;

            var multiConstructor = innerType.GetConstructors()
                .Where(cons => cons.GetParameters().Length == constructorParams.Length);

            var staticParseMethod = innerType.GetMethod("Parse",
                                        BindingFlags.Static | BindingFlags.Public,
                                        null,
                                        new Type[] { typeof(string) },
                                        null);

            var stringConstructor = innerType.GetConstructor(new[] { typeof(string) });



            ExtractionPlanNode node;

            if (IsList(innerType))
                node = new ListOfListsNode(groupName, type, constructorParams, propertySetters);
            else if (IsTuple(innerType))
                node = new ConstructTupleNode(groupName, type, constructorParams, propertySetters);
            else if (multiConstructor.Count() == 1 && (constructorParams.Any() || propertySetters.Any()))
                node = new ConstructorNode(groupName, type, constructorParams, propertySetters);
            else if (staticParseMethod is not null)
                node = new StaticParseMethodNode(groupName, type, constructorParams, propertySetters);
            else if (stringConstructor is not null)
                node = new StringConstructorNode(groupName, type, constructorParams, propertySetters);
            else if (innerType.BaseType == typeof(Enum))
                node = new EnumParseNode(groupName, type, constructorParams, propertySetters);
            else
                node = new StringCastNode(groupName, type, constructorParams, propertySetters);

            node.Validate();

            return node;
        }

        internal virtual void Validate()
        {
            return;
        }

        internal virtual object? Construct(Match match, Type type, (string Value, int Index, int Length) range)
        {
            throw new InvalidOperationException("Can't construct a node based on base ExtractionPlanNode type.");
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

                    if (result is not null)
                    {
                        foreach (var prop in propertyNodes)
                        {
                            result.GetType().GetProperty(prop.groupName).GetSetMethod().Invoke(result, new[] { prop.Execute(match, lastRange.Index, lastRange.Length) });
                        }
                    }
                }
            }
            else
            {
                var listType = type.GetGenericArguments().Single();

                List<object?> vals = new();
                
                foreach (var range in ranges)
                {
                    var itemVal = Construct(match, listType, range);

                    if (itemVal is not null)
                    {
                        foreach (var prop in propertyNodes)
                        {
                            itemVal.GetType().GetProperty(prop.groupName).GetSetMethod().Invoke(result, new[] { prop.Execute(match, range.Index, range.Length) });
                        }
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
