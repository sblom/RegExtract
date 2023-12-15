using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using RegExtract.ExtractionPlanNodeTypes;
using RegExtract.RegexTools;

namespace RegExtract
{
    public class ExtractionPlan<T>: IFormattable
    {
        public ExtractionPlanNode Plan { get; protected set; }
        RegexCaptureGroupTree? _tree;
        Stack<Type> _typeStack = new();

        protected ExtractionPlan()
        {
            Plan = new UninitializedNode();
        }

        public T Extract(string str)
        {
            return (T)Plan.Execute(_tree?.Regex.Match(str) ?? Regex.Match("",""))!;
        }

        public T Extract(Match match)
        {
            return (T)Plan.Execute(match)!;
        }

        public bool TryExtract(string str, out T result)
        {
            result = default!;
            if (!Plan.TryExecute(_tree?.Regex.Match(str) ?? Regex.Match("",""), out var temp))
            {
                return false;
            }
            result = (T)temp!;
            return true;
        }

        public bool TryExtract(Match match, out T result)
        {
            result = default!;
            if (!Plan.TryExecute(match, out var temp))
            {
                return false;
            }
            result = (T)temp!;
            return true;
        }

        static public ExtractionPlan<T> CreatePlan(Regex regex, RegExtractOptions reOptions= RegExtractOptions.None)
        {
            ExtractionPlan<T> plan = new ExtractionPlan<T>();
            plan.InitializePlan(regex);

            return plan;
        }

        internal void InitializePlan(Regex regex)
        {
            _tree = new RegexCaptureGroupTree(regex);
            Type type = typeof(T);

            Plan = AssignTypesToTree_0(_tree.Tree, type);
        }


        protected const string VALUETUPLE_TYPENAME = "System.ValueTuple`";
        protected const string NULLABLE_TYPENAME = "System.Nullable`";

        protected bool IsCollection(Type type)
        {
            return type.GetInterfaces()
                       .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>));
        }

        protected bool IsTuple(Type type)
        {
            return type.FullName.StartsWith(VALUETUPLE_TYPENAME);
        }

        protected bool IsNullable(Type type)
        {
            return type.FullName.StartsWith(NULLABLE_TYPENAME);
        }

        private bool IsContainerOfSize(Type type, int numParams)
        {
            var constructors = type.GetConstructors()
                .Where(cons => cons.GetParameters().Length == numParams);

            return constructors.Count() == 1;
        }

        protected bool IsDirectlyConstructable(Type type)
        {
            if (type == typeof(string))
            {
                return true;
            }

            if (IsCollection(type))
            {
                type = type.GetGenericArguments().Single();
                return !IsCollection(type) && IsDirectlyConstructable(type);
            }

            if (IsNullable(type))
            {
                type = type.GetGenericArguments().Single();
            }

            if (IsTuple(type))
            {
                return false;
            }

            var parse = type.GetMethod("Parse",
                            BindingFlags.Static | BindingFlags.Public,
                            null,
                            new Type[] { typeof(string) },
                            null);

            if (parse is not null)
            {
                return true;
            }

            if (type.BaseType == typeof(Enum))
            {
                return true;
            }

            return false;
        }

        protected Type[] GetTupleArgumentsList(Type type)
        {
            var typeArgs = type.GetGenericArguments();

            if (IsTuple(type) && typeArgs.Length == 8)
            {
                return typeArgs.Take(7).Concat(GetTupleArgumentsList(typeArgs[7])).ToArray();
            }
            else
            {
                return typeArgs;
            }
        }

        private ExtractionPlanNode AssignTypesToTree_0(RegexCaptureGroupNode tree, Type type)
        {
            var unwrappedType = IsCollection(type) ? type.GetGenericArguments().Single() : type;
            unwrappedType = IsNullable(unwrappedType) ? unwrappedType.GetGenericArguments().Single() : unwrappedType;

            if (!tree.children.Any())
            {
                return ExtractionPlanNode.BindLeaf("0", type, new ExtractionPlanNode[0], new ExtractionPlanNode[0]);
            }

            if (!IsTuple(unwrappedType) && !IsContainerOfSize(unwrappedType, tree.NumberedGroups.Count()) && !tree.NamedGroups.Any())
            {
                return new VirtualUnaryTupleNode(tree.name, type, new ExtractionPlanNode[] { AssignTypesToTree_Recursive(tree.children.Single(), type) }, new ExtractionPlanNode[0]);
            }

            return AssignTypesToTree_Recursive(tree, type);
        }

        ExtractionPlanNode BindPropertyPlan(RegexCaptureGroupNode tree, Type type, string name)
        {
            if (IsNullable(type))
            {
                type = type.GetGenericArguments().Single();
            }

            var property = type.GetProperty(name);

            if (property is null)
                throw new ArgumentException($"Could not find property for named capture group '{name}'.");

            type = property.PropertyType;

            return AssignTypesToTree_Recursive(tree, type);
        }

        ExtractionPlanNode BindConstructorPlan(RegexCaptureGroupNode tree, Type type, int paramNum, int paramCount, Stack<RegexCaptureGroupNode>? stack)
        {
            if (IsCollection(type))
            {
                type = type.GetGenericArguments().Single();
            }

            if (IsNullable(type))
            {
                type = type.GetGenericArguments().Single();
            }

            var constructors = type.GetConstructors()
                       .Where(cons => cons.GetParameters().Length == paramCount);

            if (IsTuple(type))
            {
                try
                {
                    type = GetTupleArgumentsList(type)[paramNum];
                }
                catch (IndexOutOfRangeException)
                {
                    throw new ArgumentException($"Capture group '{tree.name}' represents too many parameters for tuple {type.FullName}");
                }
            }
            else if (constructors?.Count() == 1)
            {
                var constructor = constructors.Single();

                try
                {
                    type = constructor.GetParameters()[paramNum].ParameterType;
                }
                catch (IndexOutOfRangeException)
                {
                    throw new ArgumentException($"Capture group '{tree.name}' represents too many parameters for constructor {type.FullName}");
                }
            }

            return AssignTypesToTree_Recursive(tree, type, stack);
        }

        private ExtractionPlanNode AssignTypesToTree_Recursive(RegexCaptureGroupNode tree, Type type, Stack<RegexCaptureGroupNode>? stack = null)
        {
            var unwrappedType = IsCollection(type) ? type.GetGenericArguments().Single() : type;
            unwrappedType = IsNullable(unwrappedType) ? unwrappedType.GetGenericArguments().Single() : unwrappedType;

            List<ExtractionPlanNode> groups = new();
            List<ExtractionPlanNode> namedgroups = new();

            if (!tree.children.Any() || IsDirectlyConstructable(type))
            {
                if (tree.children.Any())
                {
                    if (stack == null) throw new ArgumentException("Leftover branch in Rx subtree but no tuple with extra slots to receive it.");
                    foreach (var child in tree.children.Reverse())
                    {
                        stack.Push(child);
                    }
                }
                return ExtractionPlanNode.BindLeaf(tree.name, type, groups.ToArray(), namedgroups.ToArray());
            }
            else if (IsTuple(unwrappedType) && GetTupleArgumentsList(unwrappedType).Count() > tree.children.Where(child => int.TryParse(child.name, out var _)).Count())
            {
                stack = new Stack<RegexCaptureGroupNode>(tree.children.Reverse());

                while (stack.Any())
                {
                    //var tupleParamCount = IsTuple(type) ? type.
                    RegexCaptureGroupNode node = stack.Pop();

                    if (int.TryParse(node.name, out var num))
                    {
                        var plan = BindConstructorPlan(node, type, groups.Count, GetTupleArgumentsList(type).Count(), stack);
                        groups.Add(plan);
                    }
                    else
                    {
                        namedgroups.Add(BindPropertyPlan(node, type, node.name));
                    }
                }
            }
            else
            {
                foreach (var node in tree.children)
                {
                    if (int.TryParse(node.name, out var num))
                    {
                        var plan = BindConstructorPlan(node, type, groups.Count, tree.NumberedGroups.Count(), stack);
                        groups.Add(plan);
                    }
                    else
                    {
                        namedgroups.Add(BindPropertyPlan(node, type, node.name));
                    }

                }
            }

            return ExtractionPlanNode.Bind(tree.name, type, groups.ToArray(), namedgroups.ToArray());
        }

        public object ToDump() => this;

        public override string ToString() =>
            Plan.ShowPlanTree().Replace("\t", "").Replace("\n", "");

        public string ToString(string? format) => ToString(format, null);

        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            if (format == "x")
                return Plan.ShowPlanTree() + "\n\n" + _tree?.TreeViz() ?? "";
            else return Plan.ShowPlanTree().Replace("\t", "").Replace("\n", "");
        }
    }
}
