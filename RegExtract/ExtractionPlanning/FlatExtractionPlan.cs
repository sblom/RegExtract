using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RegExtract
{
    public class FlatExtractionPlan<T> : ExtractionPlan<T>
    {
        RegexCaptureGroupTree _tree;
        Stack<Type> _typeStack = new();

        internal override void InitializePlan(Regex regex)
        {
            _tree = new RegexCaptureGroupTree(regex);
            Type type = typeof(T);

            Plan = AssignTypesToTree_0(_tree.Tree, type);
        }

        private ExtractionPlanNode AssignTypesToTree_0(RegexCaptureGroupNode tree, Type type)
        {
            if (!tree.children.Any())
            {
                return new ExtractionPlanNode(tree.name, type, new ExtractionPlanNode[0], new ExtractionPlanNode[0]);
            }

            // TODO: Really need to think this through, and think lists through in general. I'm pretty sure there are still subtle list bugs around.
            if ((ArityOfType(type) == 1 && !tree.NamedGroups.Any())|| (IsList(type) && IsList(type.GetGenericArguments().Single())))
            {
                return new RootVirtualTupleExtractionPlanNode(tree.name, type, new ExtractionPlanNode[] { AssignTypesToTree_Recursive(tree.children.Single(), type).Item1 }, new ExtractionPlanNode[0]);
            }

            return AssignTypesToTree_Recursive(tree, type).Item1;
        }

        (ExtractionPlanNode, RegexCaptureGroupNode[]) BindPropertyPlan(RegexCaptureGroupNode tree, Type type, string name)
        {
            if (type.FullName.StartsWith(NULLABLE_TYPENAME))
            {
                type = type.GetGenericArguments().Single();
            }

            var property = type.GetProperty(name);

            if (property is null)
                throw new ArgumentException($"Could not find property for named capture group '{name}'.");

            type = property.PropertyType;

            return AssignTypesToTree_Recursive(tree, type);
        }

        (ExtractionPlanNode, RegexCaptureGroupNode[]) BindConstructorPlan(RegexCaptureGroupNode tree, Type type, int paramNum)
        {
            if (IsNullable(type))
            {
                type = type.GetGenericArguments().Single();
            }

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

            if (type.FullName.StartsWith(VALUETUPLE_TYPENAME))
            {
                type = GetTupleArgumentsList(type)[paramNum];
            }
            else if (constructors?.Count() == 1)
            {
                var constructor = constructors.Single();

                type = constructor.GetParameters()[paramNum].ParameterType;
            }

            return AssignTypesToTree_Recursive(tree, type);
        }

        private (ExtractionPlanNode, RegexCaptureGroupNode[]) AssignTypesToTree_Recursive(RegexCaptureGroupNode tree, Type type)
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
                        var constructorPlan = BindConstructorPlan(node, type, groups.Count);
                        groups.Add(constructorPlan.Item1);
                        foreach (var extra in constructorPlan.Item2)
                        {
                            queue.Enqueue(extra);
                        }
                    }
                    else
                    {
                        namedgroups.Add(BindPropertyPlan(node, type, node.name).Item1);
                    }
                }
            }

            return (new ExtractionPlanNode(tree.name, type, groups.ToArray(), namedgroups.ToArray()), queue.ToArray());
        }

    }
}
