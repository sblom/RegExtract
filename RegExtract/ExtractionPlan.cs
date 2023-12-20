using System;
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
        public ExtractionPlanNode Plan { get; protected set; }
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
            Type type = typeof(T);

            Plan = AssignTypesToTree(_tree.Tree, type);
        }


        protected const string VALUETUPLE_TYPENAME = "System.ValueTuple`";
        protected const string NULLABLE_TYPENAME = "System.Nullable`";

        // We use C#'s definition of an initializable collection, which is any type that implements IEnumerable and has a public Add() method.
        // In our case, we also require that the Add() method has parameters of the same type as the collection's generic parameters.
        protected bool IsInitializableCollection(Type? type)
        {
            if (type == null)
                return false;
            var genericParameters = type.GetGenericArguments();
            var addMethod = type.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance, null, genericParameters, null);

            return type.GetInterfaces().Any(i => i == typeof(IEnumerable)) && addMethod != null;
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

            return AssignTypesToTree(tree, type);
        }

        ExtractionPlanNode BindConstructorPlan(RegexCaptureGroupNode tree, Type type, int paramNum, int paramCount)
        {
            if (IsNullable(type))
            {
                type = type.GetGenericArguments().Single();
            }

            var constructors = type.GetConstructors()
                       .Where(cons => cons.GetParameters().Length == paramCount);

            if (IsInitializableCollection(type))
            {
                try
                {
                    type = type.GetGenericArguments()[paramNum];
                }
                catch (IndexOutOfRangeException)
                {
                    throw new ArgumentException($"Capture group '{tree.name}' represents too many parameters for collection {type.FullName}");
                }
            }
            else if (IsTuple(type))
            {
                try
                {
                    type = type.GetGenericArguments()[paramNum];
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

            return AssignTypesToTree(tree, type);
        }

        ExtractionPlanNode BindTupleConstructorPlan(string name, IEnumerable<RegexCaptureGroupNode> nodes, Type tupleType)
        {
            var typeArgs = IsNullable(tupleType) ? tupleType.GetGenericArguments().Single().GetGenericArguments() : tupleType.GetGenericArguments();

            List<ExtractionPlanNode> groups = new();

            foreach (var (node, type, idx) in nodes.Zip(typeArgs, (n, t) => (n,t)).Select(((x,i) => (x.n, x.t, i))))
            {
                if (idx < 7)
                {
                    groups.Add(BindConstructorPlan(node, tupleType, idx, typeArgs.Length));
                }
                else
                {
                    groups.Add(BindTupleConstructorPlan(name, nodes.Skip(7), type));
                }
            }

            return ExtractionPlanNode.Bind(name, tupleType, groups.ToArray(), new ExtractionPlanNode[0]);
        }

        private ExtractionPlanNode AssignTypesToTree(RegexCaptureGroupNode tree, Type type)
        {
            var unwrappedType = IsNullable(type) ? type.GetGenericArguments().Single() : type;

            List<ExtractionPlanNode> groups = new();
            List<ExtractionPlanNode> namedgroups = new();

            if (IsDirectlyConstructable(type))
            {
                // We're at a leaf--if there's an inner capture group, use it instead of everything.
                if (tree.children.Count() == 1)
                {
                    tree = tree.children.Single();
                }

                return ExtractionPlanNode.BindLeaf(tree.name, type, groups.ToArray(), namedgroups.ToArray());
            }
            else if (IsTuple(unwrappedType))
            {
                return BindTupleConstructorPlan(tree.name, tree.children, type);
            }
            else if (IsInitializableCollection(type))
            {
                var typeParams = type.GetGenericArguments();

                if (tree.name == "0")
                {
                    return new VirtualUnaryTupleNode(tree.name, type, new[] { AssignTypesToTree(tree.children.Single(), type) }, new ExtractionPlanNode[0]);
                }

                if (typeParams.Length < 2 && !IsInitializableCollection(typeParams.FirstOrDefault()))
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
