using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace RegExtract.ExtractionPlanNodeTypes
{
    internal record UninitializedNode() :
        ExtractionPlanNode("", typeof(void), new ExtractionPlanNode[0], new ExtractionPlanNode[0])
    {
        internal override object? Execute(Match match, int captureStart, int captureLength)
        {
            throw new InvalidOperationException("Extraction plan was not initialized before execution.");
        }
    }

    internal record VirtualUnaryTupleNode(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
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

    internal record ListOfListsNode(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        internal override object? Construct(Match match, Type type, (string Value, int Index, int Length) range)
        {
            return constructorParams.Single().Execute(match, range.Index, range.Length);
        }

        internal override void Validate()
        {
            if (!IsCollection(type) || !IsCollection(type.GetGenericArguments().Single()))
                throw new InvalidOperationException($"{nameof(ListOfListsNode)} assigned type other than List<List<T>>");

            base.Validate();
        }
    }

    internal record ConstructTupleNode(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        internal override object? Construct(Match match, Type type, (string Value, int Index, int Length) range)
        {
            type = IsNullable(type) ? type.GetGenericArguments().Single() : type;

            return CreateGenericTuple(type, constructorParams.Select(i => i.Execute(match, range.Index, range.Length)));
        }

        internal override void Validate()
        {
            var unwrappedType = IsCollection(type) ? type.GetGenericArguments().Single() : type;
            unwrappedType = IsNullable(unwrappedType) ? unwrappedType.GetGenericArguments().Single() : unwrappedType;

            if (!IsTuple(unwrappedType))
                throw new InvalidOperationException($"{nameof(ListOfListsNode)} assigned type other than ValueTuple<>");

            base.Validate();
        }
    }

    internal record ConstructorNode(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
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
            var unwrappedType = IsCollection(type) ? type.GetGenericArguments().Single() : type;
            unwrappedType = IsNullable(unwrappedType) ? unwrappedType.GetGenericArguments().Single() : unwrappedType;

            var constructors = unwrappedType.GetConstructors()
                .Where(cons => cons.GetParameters().Length == constructorParams.Length);

            if (constructors.Count() != 1)
                throw new InvalidOperationException($"{nameof(ConstructorNode)} has wrong number of constructor params.");

            base.Validate();
        }
    }

    internal record EnumParseNode(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        internal override object? Construct(Match match, Type type, (string Value, int Index, int Length) range)
        {
            return Enum.Parse(type, range.Value);
        }
    }

    internal record StringConstructorNode(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
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

    internal record StaticParseMethodNode(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        internal override object? Construct(Match match, Type type, (string Value, int Index, int Length) range)
        {
            type = IsCollection(type) ? type.GetGenericArguments().Single() : type;
            type = IsNullable(type) ? type.GetGenericArguments().Single() : type;
            if (type.Namespace != "System")
            {
                return type.GetMethod("Parse",
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new Type[] { typeof(string) },
                    null
                ).Invoke(null, new object[] { range.Value });
            }
            var args = new object[] { range.Value, null! };
            type.GetMethod("TryParse",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new Type[] { typeof(string), Type.GetType($"{type.FullName}&") },
                null
            ).Invoke(null, args);
            return args[1];
        }

        internal override void Validate()
        {
            var unwrappedType = IsCollection(type) ? type.GetGenericArguments().Single() : type;
            unwrappedType = IsNullable(unwrappedType) ? unwrappedType.GetGenericArguments().Single() : unwrappedType;

            var parse = unwrappedType.GetMethod("Parse",
                            BindingFlags.Static | BindingFlags.Public,
                            null,
                            new Type[] { typeof(string) },
                            null);

            if (parse is null)
                throw new InvalidOperationException($"{nameof(StaticParseMethodNode)} has wrong type or constructor params.");

            base.Validate();
        }
    }

    internal record StringCastNode(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        internal override object? Construct(Match match, Type type, (string Value, int Index, int Length) range)
        {
            return range.Value;
        }
    }
}
