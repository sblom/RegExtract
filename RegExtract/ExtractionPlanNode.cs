﻿using System;
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
            var innerType = IsCollection(type) ? type.GetGenericArguments().Single() : type;
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

            if (IsCollection(innerType))
                node = new ListOfListsNode(groupName, type, constructorParams, propertySetters);
            else if (IsTuple(innerType))
                node = new ConstructTupleNode(groupName, type, constructorParams, propertySetters);
            else if (multiConstructor.Count() == 1 && (constructorParams.Any() || propertySetters.Any()))
                node = new ConstructorNode(groupName, type, constructorParams, propertySetters);
            else
                throw new ArgumentException("Couldn't find appropriate constructor for type.");

            node.Validate();

            return node;
        }

        internal static ExtractionPlanNode BindLeaf(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters)
        {
            var innerType = IsCollection(type) ? type.GetGenericArguments().Single() : type;
            innerType = IsNullable(innerType) ? innerType.GetGenericArguments().Single() : innerType;

            var staticParseMethod = innerType.GetMethod("Parse",
                                        BindingFlags.Static | BindingFlags.Public,
                                        null,
                                        new Type[] { typeof(string) },
                                        null);

            var stringConstructor = innerType.GetConstructor(new[] { typeof(string) });



            ExtractionPlanNode node;

            if (IsCollection(innerType))
                throw new ArgumentException("List of lists in type cannot be bound to leaf of regex capture group tree.");
            else if (IsTuple(innerType))
                throw new ArgumentException("Tuple in type cannot be bound to leaf of regex capture group tree.");
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

        internal virtual bool TryConstruct(Match match, Type type, (string Value, int Index, int Length) range, out object? result)
        {
            result = Construct(match, type, range);
            return true;
        }

        internal virtual object? Construct(Match match, Type type, (string Value, int Index, int Length) range)
        {
            throw new InvalidOperationException("Can't construct a node based on base ExtractionPlanNode type.");
        }

        internal virtual bool TryExecute(Match match, int captureStart, int captureLength, out object? result)
        {
            var ranges = AsEnumerable(match.Groups[groupName].Captures)
                  .Where(cap => cap.Index >= captureStart && cap.Index + cap.Length <= captureStart + captureLength)
                  .Select(cap => (cap.Value, cap.Index, cap.Length));
            Type innerType = IsNullable(type) ? type.GetGenericArguments().Single() : type;
            bool isCollection = IsCollection(type);
            if (!isCollection)
            {
                if (!ranges.Any())
                {
                    if (type.IsClass || Nullable.GetUnderlyingType(type) != null)
                    {
                        result = null;
                        return false;
                    }
                    result = Convert.ChangeType(null, type);
                    return true;
                }
                else
                {
                    var lastRange = ranges.Last();
                    if (!TryConstruct(match, innerType, lastRange, out result))
                    {
                        return false;
                    }
                    foreach (var prop in propertyNodes)
                    {
                        result!.GetType().GetProperty(prop.groupName).GetSetMethod().Invoke(result, new[] { prop.Execute(match, lastRange.Index, lastRange.Length) });
                    }
                }
            }
            else
            {
                result = null;
                var itemType = type.GetGenericArguments().Single();
                var vals = Activator.CreateInstance(type);
                var addMethod = type.GetMethod("Add");
                foreach (var range in ranges)
                {
                    if (!TryConstruct(match, itemType, range, out var itemVal))
                    {
                        return false;
                    }
                    foreach (var prop in propertyNodes)
                    {
                        itemVal!.GetType().GetProperty(prop.groupName).GetSetMethod().Invoke(result, new[] { prop.Execute(match, range.Index, range.Length) });
                    }
                    addMethod.Invoke(vals, new[] { itemVal });
                }

                result = vals;
            }
            return true;
        }

        public bool TryExecute(Match match, out object? result)
        {
            if (!match.Success)
            {
                throw new ArgumentException("Regex didn't match.");
            }

            return TryExecute(match, match.Groups[groupName].Index, match.Groups[groupName].Length, out result);
        }

        internal virtual object? Execute(Match match, int captureStart, int captureLength)
        {
            object? result = null;

            var ranges = AsEnumerable(match.Groups[groupName].Captures)
                  .Where(cap => cap.Index >= captureStart && cap.Index + cap.Length <= captureStart + captureLength)
                  .Select(cap => (cap.Value, cap.Index, cap.Length));

            Type innerType = IsNullable(type) ? type.GetGenericArguments().Single() : type;

            bool isCollection = IsCollection(type);

            if (!isCollection)
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
                var itemType = type.GetGenericArguments().Single();

                var vals = Activator.CreateInstance(type);
                var addMethod = type.GetMethod("Add");
                
                foreach (var range in ranges)
                {
                    var itemVal = Construct(match, itemType, range);

                    if (itemVal is not null)
                    {
                        foreach (var prop in propertyNodes)
                        {
                            itemVal.GetType().GetProperty(prop.groupName).GetSetMethod().Invoke(result, new[] { prop.Execute(match, range.Index, range.Length) });
                        }
                    }

                    addMethod.Invoke(vals, new[] { itemVal });
                }

                result = vals;
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
        protected const string NULLABLE_TYPENAME = "System.Nullable`";

        protected static bool IsCollection(Type type)
        {
            return type.GetInterfaces()
                       .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>));
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
