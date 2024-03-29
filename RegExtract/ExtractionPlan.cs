﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using RegExtract.ExtractionPlanNodeTypes;
using RegExtract.RegexTools;

namespace RegExtract
{
    public class ExtractionPlan<T>: IFormattable
    {
        ExtractionPlanNode Plan { get; set; }
        RegexCaptureGroupTree? _tree;

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

        static public ExtractionPlan<T> CreatePlan(Regex regex, RegExtractOptions reOptions= RegExtractOptions.None)
        {
            ExtractionPlan<T> plan = new ExtractionPlan<T>();
            plan.InitializePlan(regex);

            return plan;
        }

        internal void InitializePlan(Regex regex)
        {
            _tree = new RegexCaptureGroupTree(regex);
            var type = ExtractionPlanTypeWrapper.Wrap(typeof(T));

            Plan = AssignTypesToTree(_tree.Tree, type);
        }


        ExtractionPlanNode BindPropertyPlan(RegexCaptureGroupNode tree, ExtractionPlanTypeWrapper type, string name)
        {
            type = type.NonNullableType;

            // TODO: Figure out how to move this into ExtractionPlanTypeWrapper, and do some caching
            var property = type.Type.GetProperty(name);

            if (property is null)
                throw new ArgumentException($"Could not find property for named capture group '{name}'.");

            type = ExtractionPlanTypeWrapper.Wrap(property.PropertyType);

            return AssignTypesToTree(tree, type);
        }

        ExtractionPlanNode BindConstructorPlan(RegexCaptureGroupNode tree, ExtractionPlanTypeWrapper type, int paramNum, int paramCount)
        {
            var constructors = type.Constructors
                       .Where(cons => cons.GetParameters().Length == paramCount);

            if (type.IsInitializableCollection)
            {
                try
                {
                    type = ExtractionPlanTypeWrapper.Wrap(type.GenericArguments?[paramNum]);
                }
                catch (IndexOutOfRangeException)
                {
                    throw new ArgumentException($"Capture group '{tree.name}' represents too many parameters for collection {type.Type.FullName}");
                }
            }
            else if (type.IsTuple)
            {
                try
                {
                    type = ExtractionPlanTypeWrapper.Wrap(type.GenericArguments?[paramNum]);
                }
                catch (IndexOutOfRangeException)
                {
                    throw new ArgumentException($"Capture group '{tree.name}' represents too many parameters for tuple {type.Type.FullName}");
                }
            }
            else if (constructors?.Count() == 1)
            {
                var constructor = constructors.Single();

                try
                {
                    type = ExtractionPlanTypeWrapper.Wrap(constructor.GetParameters()[paramNum].ParameterType);
                }
                catch (IndexOutOfRangeException)
                {
                    throw new ArgumentException($"Capture group '{tree.name}' represents too many parameters for constructor {type.Type.FullName}");
                }
            }

            return AssignTypesToTree(tree, type);
        }

        ExtractionPlanNode BindTupleConstructorPlan(string name, IEnumerable<RegexCaptureGroupNode> nodes, ExtractionPlanTypeWrapper tupleType)
        {
            var typeArgs = tupleType.GenericArguments;

            List<ExtractionPlanNode> groups = new();

            foreach (var (node, type, idx) in nodes.Zip(typeArgs, (n, t) => (n,t)).Select(((x,i) => (x.n, x.t, i))))
            {
                if (idx < 7)
                {
                    groups.Add(BindConstructorPlan(node, tupleType, idx, typeArgs?.Length ?? 0));
                }
                else
                {
                    groups.Add(BindTupleConstructorPlan(name, nodes.Skip(7), ExtractionPlanTypeWrapper.Wrap(type)));
                }
            }

            return ExtractionPlanNode.Bind(name, tupleType, groups.ToArray(), new ExtractionPlanNode[0]);
        }

        private ExtractionPlanNode AssignTypesToTree(RegexCaptureGroupNode tree, ExtractionPlanTypeWrapper type)
        {
            List<ExtractionPlanNode> groups = new();
            List<ExtractionPlanNode> namedgroups = new();

            if (tree.children is [] or [{children: []}] && type.IsDirectlyConstructable)
            {
                // We're at a leaf in the type hierarchy, and all we need is a string.
                // If there's an inner capture group, use it to narrow the match.
                if (tree.children.Length == 1)
                {
                    tree = tree.children.Single();
                }

                return ExtractionPlanNode.BindLeaf(tree.name, type, groups.ToArray(), namedgroups.ToArray());
            }
            else if (type.IsTuple)
            {
                return BindTupleConstructorPlan(tree.name, tree.children, type);
            }
            else if (type.IsInitializableCollection)
            {
                var typeParams = type.GenericArguments;

                if (tree.name == "0")
                {
                    return AssignTypesToTree(tree.children.Single(), type);
                }

                if ((typeParams?.Length ?? 0) < 2 && !((typeParams?.Length ?? 0) > 0 && ExtractionPlanTypeWrapper.Wrap(typeParams.First()).IsInitializableCollection))
                {
                    return ExtractionPlanNode.Bind(tree.name, type, new[] { BindConstructorPlan(tree, type, 0, 1) }, new ExtractionPlanNode[0]);
                }

                foreach (var node in tree.children)
                {
                    var plan = BindConstructorPlan(node, type, groups.Count, tree.NumberedGroups.Count());
                    groups.Add(plan);
                }
                // TODO: assert that there are no named groups
            }
            else
            {
                foreach (var node in tree.children)
                {
                    if (int.TryParse(node.name, out var num))
                    {
                        var plan = BindConstructorPlan(node, type, groups.Count, tree.NumberedGroups.Count());
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
