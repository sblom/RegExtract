using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using RegExtract.ExtractionPlanNodeTypes;

namespace RegExtract
{
    internal record ExtractionPlanNode(string groupName, ExtractionPlanTypeWrapper type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertyNodes)
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

        string FriendlyTypeName(ExtractionPlanTypeWrapper type)
        {
            var keyword = type.Type.Name switch
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

            if (type.IsNullable) return FriendlyTypeName(type.NonNullableType) + "?";

            var args = type.GenericArguments;

            if (type.IsTuple) return "(" + String.Join(",", args.Select(arg => ExtractionPlanTypeWrapper.Wrap(arg))) + ")";

            if (args.Any())
            {
                return type.Type.Name.Split('`')[0] + "<" + String.Join(",", args.Select(arg => ExtractionPlanTypeWrapper.Wrap(arg))) + ">";
            }

            else return type.Type.Name;
        }

        internal static ExtractionPlanNode Bind(string groupName, ExtractionPlanTypeWrapper type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters)
        {
            var innerType = type.NonNullableType;

            var multiConstructor = innerType.Constructors
                .Where(cons => cons.GetParameters().Length == constructorParams.Length);

            var staticParseMethod = innerType.Type.GetMethod("Parse",
                                        BindingFlags.Static | BindingFlags.Public,
                                        null,
                                        new Type[] { typeof(string) },
                                        null);

            var stringConstructor = innerType.Type.GetConstructor(new[] { typeof(string) });



            ExtractionPlanNode node;

            if (type.IsInitializableCollection)
                node = new CollectionInitializerNode(groupName, type, constructorParams, propertySetters);
            else if (innerType.IsTuple)
                node = new ConstructTupleNode(groupName, type, constructorParams, propertySetters);
            else if (multiConstructor.Count() == 1 && (constructorParams.Any() || propertySetters.Any()))
                node = new ConstructorNode(groupName, type, constructorParams, propertySetters);
            else if (!constructorParams.Any() && !propertySetters.Any() && stringConstructor != null)
                node = new StringConstructorNode(groupName, type, new ExtractionPlanNode[0], new ExtractionPlanNode[0]);
            else
                throw new ArgumentException("Couldn't find appropriate constructor for type.");

            node.Validate();

            return node;
        }

        internal static ExtractionPlanNode BindLeaf(string groupName, ExtractionPlanTypeWrapper type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters)
        {
            var innerType = type.NonNullableType;

            var staticParseMethod = innerType.Type.GetMethod("Parse",
                                        BindingFlags.Static | BindingFlags.Public,
                                        null,
                                        new Type[] { typeof(string) },
                                        null);

            var stringConstructor = innerType.Type.GetConstructor(new[] { typeof(string) });



            ExtractionPlanNode node;

            if (innerType.IsTuple)
                throw new ArgumentException("Tuple in type cannot be bound to leaf of regex capture group tree.");
            else if (staticParseMethod is not null)
                node = new StaticParseMethodNode(groupName, type, constructorParams, propertySetters);
            else if (stringConstructor is not null)
                node = new StringConstructorNode(groupName, type, constructorParams, propertySetters);
            else if (innerType.Type.BaseType == typeof(Enum))
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

        internal virtual object? Construct(Match match, ExtractionPlanTypeWrapper type, (string Value, int Index, int Length) range, Dictionary<string, (string Value, int Index, int Length)[]> cache)
        {
            throw new InvalidOperationException("Can't construct a node based on base ExtractionPlanNode type.");
        }

        protected IEnumerable<(string Value, int Index, int Length)> Ranges(Match match, string groupName, int captureStart, int captureLength, Dictionary<string, (string Value, int Index, int Length)[]> cache)
        {
            if (!cache.ContainsKey(groupName))
            {
                cache[groupName] = AsEnumerable(match.Groups[groupName].Captures)
                    .Select(cap => (cap.Value, cap.Index, cap.Length))
                    .ToArray();
            }
            return cache[groupName].Where(cap => cap.Index >= captureStart && cap.Index + cap.Length <= captureStart + captureLength);
        }

        internal virtual object? Execute(Match match, int captureStart, int captureLength, Dictionary<string, (string Value, int Index, int Length)[]> cache)
        {
            object? result = null;

            var ranges = Ranges(match, groupName, captureStart, captureLength, cache).ToArray();

            if (!ranges.Any())
            {
                if (type.Type.IsClass || Nullable.GetUnderlyingType(type.Type) != null) return null;
                else return Convert.ChangeType(null, type.Type);
            }
            else
            {
                var lastRange = ranges.Last();

                result = Construct(match, type.NonNullableType, lastRange, cache);

                if (result is not null)
                {
                    foreach (var prop in propertyNodes)
                    {
                        result.GetType().GetProperty(prop.groupName).GetSetMethod().Invoke(result, new[] { prop.Execute(match, lastRange.Index, lastRange.Length, cache) });
                    }
                }
            }

            return result;
        }

        internal object? Execute(Match match)
        {
            if (!match.Success)
            {
                throw new ArgumentException("Regex didn't match.");
            }

            Dictionary<string, (string Value, int Index, int Length)[]> cache = new();

            return Execute(match, match.Groups[0].Index, match.Groups[0].Length, cache);
        }

        protected const string VALUETUPLE_TYPENAME = "System.ValueTuple`";
        protected const string NULLABLE_TYPENAME = "System.Nullable`";

        protected static IEnumerable<Capture> AsEnumerable(CaptureCollection cc)
        {
            foreach (Capture c in cc)
            {
                yield return c;
            }
        }
    }
}
