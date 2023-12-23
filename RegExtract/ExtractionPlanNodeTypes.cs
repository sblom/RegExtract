using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace RegExtract.ExtractionPlanNodeTypes
{
    internal record UninitializedNode() :
        ExtractionPlanNode("", ExtractionPlanTypeWrapper.Wrap(typeof(void)), new ExtractionPlanNode[0], new ExtractionPlanNode[0])
    {
        internal override object? Execute(Match match, int captureStart, int captureLength, Dictionary<string, (string Value, int Index, int Length)[]> cache)
        {
            throw new InvalidOperationException("Extraction plan was not initialized before execution.");
        }
    }

    internal record CollectionInitializerNode(string groupName, ExtractionPlanTypeWrapper type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        internal override object? Execute(Match match, int captureStart, int captureLength, Dictionary<string, (string Value, int Index, int Length)[]> cache)
        {
            var genericArgs = type.GenericArguments;

            // TODO: Create a pre-sized collection
            var vals = Activator.CreateInstance(type.Type);
            var addMethod = type.Type.GetMethod("Add");

            object?[] itemVals = new object[genericArgs?.Length ?? 0];

            var rangeArray = constructorParams.Select(c => Ranges(match, groupName, captureStart, captureLength, cache).GetEnumerator()).ToArray();

            do
            {
                for (int i = 0; i < genericArgs?.Length; i++)
                {
                    if (rangeArray[i].MoveNext())
                    {
                        itemVals[i] = constructorParams[i].Execute(match, rangeArray[i].Current.Index, rangeArray[i].Current.Length, cache);
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

    internal record ConstructTupleNode(string groupName, ExtractionPlanTypeWrapper type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        ConstructorInfo? _constructor = null;

        ConstructorInfo constructor
        {
            get
            {
                if (_constructor != null) return _constructor;
                else
                {
                    var wrappedType = type.NonNullableType;
                    return (_constructor = wrappedType.Type.GetConstructor(wrappedType.Type.GetGenericArguments()));
                }
            }
        }

        internal override object? Construct(Match match, ExtractionPlanTypeWrapper type, (string Value, int Index, int Length) range, Dictionary<string, (string Value, int Index, int Length)[]> cache)
        {
            type = type.NonNullableType;
            var constructor = type.Type.GetConstructor(type.Type.GetGenericArguments());

            return constructor.Invoke(constructorParams.Select(i => i.Execute(match, range.Index, range.Length, cache)).ToArray());
        }

        internal override void Validate()
        {
            base.Validate();
        }
    }

    internal record ConstructorNode(string groupName, ExtractionPlanTypeWrapper type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        ConstructorInfo? _constructor = null;

        ConstructorInfo constructor
        {
            get
            {
                return _constructor ?? type.Constructors.Where(cons => cons.GetParameters().Length == constructorParams.Length).Single();
            }
        }

        internal override object? Construct(Match match, ExtractionPlanTypeWrapper type, (string Value, int Index, int Length) range, Dictionary<string, (string Value, int Index, int Length)[]> cache)
        {
            return constructor.Invoke(constructorParams.Select(i => i.Execute(match, range.Index, range.Length, cache)).ToArray());
        }

        internal override void Validate()
        {
            var constructors = type.Constructors
                .Where(cons => cons.GetParameters().Length == constructorParams.Length);

            if (constructors.Count() != 1)
                throw new InvalidOperationException($"{nameof(ConstructorNode)} has wrong number of constructor params.");

            base.Validate();
        }
    }

    internal record EnumParseNode(string groupName, ExtractionPlanTypeWrapper type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        internal override object? Construct(Match match, ExtractionPlanTypeWrapper type, (string Value, int Index, int Length) range, Dictionary<string, (string Value, int Index, int Length)[]> cache)
        {
            return Enum.Parse(type.Type, range.Value);
        }
    }

    internal record StringConstructorNode(string groupName, ExtractionPlanTypeWrapper type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        ConstructorInfo? _constructor = null;

        ConstructorInfo constructor
        {
            get
            {
                return _constructor ?? (type.NonNullableType.Type.GetConstructor(new[] { typeof(string) }));
            }
        }


        internal override object? Construct(Match match, ExtractionPlanTypeWrapper type, (string Value, int Index, int Length) range, Dictionary<string, (string Value, int Index, int Length)[]> cache)
        {
            Debug.Assert(type == this.type);
            return constructor.Invoke(new[] { range.Value });
        }

        internal override void Validate()
        {
            var constructor = type.NonNullableType.Type.GetConstructor(new[] { typeof(string) });

            if (constructor is null || constructorParams.Length != 0)
                throw new InvalidOperationException($"{nameof(StringConstructorNode)} has wrong type or constructor params.");

            base.Validate();
        }
    }

    internal record StaticParseMethodNode(string groupName, ExtractionPlanTypeWrapper type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        MethodInfo? _parse = null;

        MethodInfo parse
        {
            get
            {
                return _parse ?? (_parse = type.NonNullableType.Type
                    .GetMethod("Parse",
                            BindingFlags.Static | BindingFlags.Public,
                            null,
                            new Type[] { typeof(string) },
                            null));
            }
        }

        internal override object? Construct(Match match, ExtractionPlanTypeWrapper type, (string Value, int Index, int Length) range, Dictionary<string, (string Value, int Index, int Length)[]> cache)
        {
            return parse.Invoke(null, new object[] { range.Value });
        }

        internal override void Validate()
        {
            var parse = type.NonNullableType.Type.GetMethod("Parse",
                            BindingFlags.Static | BindingFlags.Public,
                            null,
                            new Type[] { typeof(string) },
                            null);

            if (parse is null)
                throw new InvalidOperationException($"{nameof(StaticParseMethodNode)} has wrong type or constructor params.");

            base.Validate();
        }
    }

    internal record StringCastNode(string groupName, ExtractionPlanTypeWrapper type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        internal override object? Construct(Match match, ExtractionPlanTypeWrapper type, (string Value, int Index, int Length) range, Dictionary<string, (string Value, int Index, int Length)[]> cache)
        {
            return range.Value;
        }
    }
}
