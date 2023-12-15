using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

    internal record CollectionInitializerNode(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        internal override object? Execute(Match match, int captureStart, int captureLength)
        {
            var genericArgs = type.GetGenericArguments();

            var vals = Activator.CreateInstance(type);
            var addMethod = type.GetMethod("Add");

            object?[] itemVals = new object[genericArgs.Length];

            var rangeArray = constructorParams.Select(c => Ranges(match, groupName, captureStart, captureLength).GetEnumerator()).ToArray();

            do
            {
                for (int i = 0; i < genericArgs.Length; i++)
                {
                    if (rangeArray[i].MoveNext())
                    {
                        itemVals[i] = constructorParams[i].Execute(match, rangeArray[i].Current.Index, rangeArray[i].Current.Length);
                    }
                    else
                    {
                        goto no_more;
                    }
                }
                addMethod.Invoke(vals, itemVals);
            } while (true);
            
        no_more:;

            foreach (var range in rangeArray)
            {
                range.Dispose();
            }

            return vals;
        }
    }

    internal record ConstructTupleNode(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        private object CreateGenericTuple(Type tupleType, IEnumerable<object?> vals)
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

        internal override object? Construct(Match match, Type type, (string Value, int Index, int Length) range)
        {
            type = IsNullable(type) ? type.GetGenericArguments().Single() : type;

            return CreateGenericTuple(type, constructorParams.Select(i => i.Execute(match, range.Index, range.Length)));
        }

        internal override void Validate()
        {
            var unwrappedType = IsInitializableCollection(type) ? type.GetGenericArguments().Single() : type;
            unwrappedType = IsNullable(unwrappedType) ? unwrappedType.GetGenericArguments().Single() : unwrappedType;

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
            var unwrappedType = IsInitializableCollection(type) ? type.GetGenericArguments().Single() : type;
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
            type = IsInitializableCollection(type) ? type.GetGenericArguments().Single() : type;
            type = IsNullable(type) ? type.GetGenericArguments().Single() : type;

            var parse = type.GetMethod("Parse",
                            BindingFlags.Static | BindingFlags.Public,
                            null,
                            new Type[] { typeof(string) },
                            null);

            return parse.Invoke(null, new object[] { range.Value });
        }

        internal override void Validate()
        {
            var unwrappedType = IsInitializableCollection(type) ? type.GetGenericArguments().Single() : type;
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
