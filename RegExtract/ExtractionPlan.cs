using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace RegExtract
{
    public class ExtractionPlan<T>
    {
        public ExtractionPlanNode Plan { get; protected set; }
        RegexCaptureGroupTree? _tree;
        Stack<Type> _typeStack = new();

        protected ExtractionPlan()
        {
            Plan = new UninitializedNode();
        }

        public T Extract(Match match)
        {
            return (T)Plan.Execute(match)!;
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

        protected bool IsDirectlyConstructable(Type type)
        {
            if (type == typeof(string))
            {
                return true;
            }

            if (IsList(type))
            {
                type = type.GetGenericArguments().Single();
            }

            if (IsNullable(type))
            {
                type = type.GetGenericArguments().Single();
            }

            if (IsList(type))
            {
                return false;
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

            var constructor = type.GetConstructor(new[] { typeof(string) });

            if (constructor is not null)
            {
                return true;
            }

            if (type.BaseType == typeof(Enum))
            {
                return true;
            }

            return false;
        }

        protected int ArityOfType(Type type, bool recursive = false)
        {
            ConstructorInfo[] constructors;

            if (IsNullable(type))
            {
                return ArityOfType(type.GetGenericArguments().Single());
            }
            else if (IsList(type))
            {
                var subtype = type.GetGenericArguments().Single();
                if (IsList(subtype))
                {
                    return 1 + ArityOfType(subtype);
                }
                else
                {
                    return ArityOfType(subtype);
                }
            }
            else if (IsTuple(type))
            {
                return 1 + GetTupleArgumentsList(type).Sum(type => ArityOfType(type));
            }
            else
            {
                constructors = type.GetConstructors().Where(cons => cons.GetParameters().Length != 0).ToArray();
                if (constructors.Length == 1)
                {
                    return 1 + constructors[0].GetParameters().Sum(type => ArityOfType(type.ParameterType));
                }
                else
                {
                    return 1;
                }
            }
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
            if (!tree.children.Any())
            {
                return ExtractionPlanNode.Bind(tree.name, type, new ExtractionPlanNode[0], new ExtractionPlanNode[0]);
            }

            // TODO: Really need to think this through, and think lists through in general. I'm pretty sure there are still subtle list bugs around.
            if ((ArityOfType(type) == 1 && !tree.NamedGroups.Any()) || (IsList(type) && IsList(type.GetGenericArguments().Single())))
            {
                return new VirtualUnaryTupleNode(tree.name, type, new ExtractionPlanNode[] { AssignTypesToTree_Recursive(tree.children.Single(), type).node }, new ExtractionPlanNode[0]);
            }

            var (plan, remainder) = AssignTypesToTree_Recursive(tree, type);

            if (remainder.Any()) throw new ArgumentException("Provided type did not consume all regular expression captures.");

            return plan;
        }

        (ExtractionPlanNode node, RegexCaptureGroupNode[] leftoverSubtree) BindPropertyPlan(RegexCaptureGroupNode tree, Type type, string name)
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

        (ExtractionPlanNode node, RegexCaptureGroupNode[] leftoverSubtree) BindConstructorPlan(RegexCaptureGroupNode tree, Type type, int paramNum)
        {
            if (IsList(type))
            {
                type = type.GetGenericArguments().Single();
            }

            if (IsNullable(type))
            {
                type = type.GetGenericArguments().Single();
            }

            var constructors = type.GetConstructors()
                       .Where(cons => cons.GetParameters().Length != 0);

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

            return AssignTypesToTree_Recursive(tree, type);
        }

        private (ExtractionPlanNode node, RegexCaptureGroupNode[] leftoverSubtree) AssignTypesToTree_Recursive(RegexCaptureGroupNode tree, Type type)
        {
            List<ExtractionPlanNode> groups = new();
            List<ExtractionPlanNode> namedgroups = new();

            Queue<RegexCaptureGroupNode> queue = new Queue<RegexCaptureGroupNode>(tree.children);

            if (!IsDirectlyConstructable(type))
            {
                while (queue.Any())
                {
                    RegexCaptureGroupNode node = queue.Dequeue();

                    if (int.TryParse(node.name, out var num))
                    {
                        var (plan, extras) = BindConstructorPlan(node, type, groups.Count);
                        groups.Add(plan);
                        foreach (var extra in extras)
                        {
                            queue.Enqueue(extra);
                        }
                    }
                    else
                    {
                        namedgroups.Add(BindPropertyPlan(node, type, node.name).node);
                    }
                }
            }

            return (ExtractionPlanNode.Bind(tree.name, type, groups.ToArray(), namedgroups.ToArray()), queue.ToArray());
        }
    }
}
