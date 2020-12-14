using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RegExtract
{
    public class TreeExtractionPlan<T> : ExtractionPlan<T>
    {
        RegexCaptureGroupTree _tree;

        internal override void InitializePlan(Regex regex)
        {
            _tree = new RegexCaptureGroupTree(regex);
            Type type = typeof(T);

            Plan = AssignTypesToTreeRoot(_tree.Tree, type);
        }

        private ExtractionPlanNode AssignTypesToTreeRoot(RegexCaptureGroupNode tree, Type type)
        {
            if (!tree.children.Any())
            {
                return new ExtractionPlanNode(tree.name, type, new ExtractionPlanNode[0], new ExtractionPlanNode[0]);
            }

            // TODO: Really need to think this through, and think lists through in general. I'm pretty sure there are still subtle list bugs around.
            if (ArityOfType(type) == 1 || (IsList(type) && IsList(type.GetGenericArguments().Single())))
            {
                return new RootVirtualTupleExtractionPlanNode(tree.name, type, new ExtractionPlanNode[] { AssignTypesToTree(tree.children.Single(), type) }, new ExtractionPlanNode[0]);
            }

            return AssignTypesToTree(tree, type);
        }

        ExtractionPlanNode BindPropertyPlan(RegexCaptureGroupNode tree, Type type, string name)
        {
            if (type.FullName.StartsWith(NULLABLE_TYPENAME))
            {
                type = type.GetGenericArguments().Single();
            }

            var property = type.GetProperty(name);

            if (property is null)
                throw new ArgumentException($"Could not find property for named capture group '{name}'.");

            type = property.PropertyType;

            return AssignTypesToTree(tree, type);
        }

        ExtractionPlanNode BindConstructorPlan(RegexCaptureGroupNode tree, Type type, int paramNum)
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

            return AssignTypesToTree(tree, type);
        }


        private ExtractionPlanNode AssignTypesToTree(RegexCaptureGroupNode tree, Type type)
        {
            List<ExtractionPlanNode> groups = new();
            List<ExtractionPlanNode> namedgroups = new();

            foreach (var node in tree.children)
            {
                if (int.TryParse(node.name, out var num))
                {
                    groups.Add(BindConstructorPlan(node, type, groups.Count));
                }
                else
                {
                    namedgroups.Add(BindPropertyPlan(node, type, tree.name));
                }
            }

            return new ExtractionPlanNode(tree.name, type, groups.ToArray(), namedgroups.ToArray());
        }


        internal TreeExtractionPlan()
        {
        }

    }
}
