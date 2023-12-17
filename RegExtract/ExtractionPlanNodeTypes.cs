using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace RegExtract.ExtractionPlanNodeTypes
{
    internal record UninitializedNode() :
        ExtractionPlanNode("", typeof(void), new ExtractionPlanNode[0], new ExtractionPlanNode[0])
    {
        internal override object? Execute(Match match, int captureStart, int captureLength, Dictionary<string, (string Value, int Index, int Length)[]> cache)
        {
            throw new InvalidOperationException("Extraction plan was not initialized before execution.");
        }
    }

    internal record VirtualUnaryTupleNode(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        internal override object? Execute(Match match, int captureStart, int captureLength, Dictionary<string, (string Value, int Index, int Length)[]> cache)
        {
            return constructorParams.Single().Execute(match, captureStart, captureLength, cache);
        }

        internal override void Validate()
        {
            base.Validate();
        }
    }

    internal record CollectionInitializerNode(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        internal override object? Execute(Match match, int captureStart, int captureLength, Dictionary<string, (string Value, int Index, int Length)[]> cache)
        {
            var genericArgs = type.GetGenericArguments();

            // TODO: Create a pre-sized collection
            var vals = Activator.CreateInstance(type);
            var addMethod = type.GetMethod("Add");

            object?[] itemVals = new object[genericArgs.Length];

            var rangeArray = constructorParams.Select(c => Ranges(match, groupName, captureStart, captureLength, cache).GetEnumerator()).ToArray();

            do
            {
                for (int i = 0; i < genericArgs.Length; i++)
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

    internal record ConstructTupleNode(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
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
                    var wrappedType = IsNullable(type) ? type.GetGenericArguments().Single() : type;
                    return (_constructor = wrappedType.GetConstructor(wrappedType.GetGenericArguments()));
                }
            }
        }

        internal override object? Construct(Match match, Type type, (string Value, int Index, int Length) range, Dictionary<string, (string Value, int Index, int Length)[]> cache)
        {
            type = IsNullable(type) ? type.GetGenericArguments().Single() : type;
            var constructor = type.GetConstructor(type.GetGenericArguments());

            return constructor.Invoke(constructorParams.Select(i => i.Execute(match, range.Index, range.Length, cache)).ToArray());
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
        ConstructorInfo? _constructor = null;

        ConstructorInfo constructor
        {
            get
            {
                return _constructor ?? (_constructor = (IsNullable(type) ? type.GetGenericArguments().Single() : type).GetConstructors().Where(cons => cons.GetParameters().Length == constructorParams.Length).Single());
            }
        }

        internal override object? Construct(Match match, Type type, (string Value, int Index, int Length) range, Dictionary<string, (string Value, int Index, int Length)[]> cache)
        {
            return constructor.Invoke(constructorParams.Select(i => i.Execute(match, range.Index, range.Length, cache)).ToArray());
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
        internal override object? Construct(Match match, Type type, (string Value, int Index, int Length) range, Dictionary<string, (string Value, int Index, int Length)[]> cache)
        {
            return Enum.Parse(type, range.Value);
        }
    }

    internal record StringConstructorNode(string groupName, Type type, ExtractionPlanNode[] constructorParams, ExtractionPlanNode[] propertySetters) :
        ExtractionPlanNode(groupName, type, constructorParams, propertySetters)
    {
        ConstructorInfo? _constructor = null;

        ConstructorInfo constructor
        {
            get
            {
                return _constructor ?? (_constructor = (IsNullable(type) ? type.GetGenericArguments().Single() : type).GetConstructor(new[] { typeof(string) }));
            }
        }


        internal override object? Construct(Match match, Type type, (string Value, int Index, int Length) range, Dictionary<string, (string Value, int Index, int Length)[]> cache)
        {
            Debug.Assert(type == this.type);
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
        MethodInfo? _parse = null;

        MethodInfo parse
        {
            get
            {
                return _parse ?? (_parse = (IsNullable(type) ? type.GetGenericArguments().Single() : type)
                    .GetMethod("Parse",
                            BindingFlags.Static | BindingFlags.Public,
                            null,
                            new Type[] { typeof(string) },
                            null));
            }
        }

        internal override object? Construct(Match match, Type type, (string Value, int Index, int Length) range, Dictionary<string, (string Value, int Index, int Length)[]> cache)
        {
            type = IsNullable(type) ? type.GetGenericArguments().Single() : type;

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
        internal override object? Construct(Match match, Type type, (string Value, int Index, int Length) range, Dictionary<string, (string Value, int Index, int Length)[]> cache)
        {
            return range.Value;
        }
    }
}
