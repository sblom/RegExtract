using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RegExtract
{
    internal class ExtractionPlanTypeWrapper
    {
        private static Dictionary<Type, ExtractionPlanTypeWrapper> _typeWrappers = new();

        public static ExtractionPlanTypeWrapper Wrap(Type type)
        {
            if (_typeWrappers.ContainsKey(type))
                return _typeWrappers[type];
            else
                return (_typeWrappers[type] = new ExtractionPlanTypeWrapper(type));
        }

        private ExtractionPlanTypeWrapper(Type type)
        {
            Type = type;
            _genericArguments = new Lazy<Type[]?>(() => NonNullableType.Type.GetGenericArguments());
            _addMethod = new Lazy<MethodInfo?>(() => Type.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance, null, GenericArguments, null));
            _nonNullableType = new Lazy<ExtractionPlanTypeWrapper>(() => { var type = Nullable.GetUnderlyingType(Type); return type != null ? ExtractionPlanTypeWrapper.Wrap(type) : this; });
        }

        public Type Type { get; }

        private Lazy<ExtractionPlanTypeWrapper> _nonNullableType;
        public ExtractionPlanTypeWrapper NonNullableType => _nonNullableType.Value;

        private Lazy<Type[]?> _genericArguments;
        public Type[]? GenericArguments => _genericArguments.Value;

        private bool? _isNullable = null;
        public bool IsNullable => _isNullable.HasValue ? _isNullable.Value : ((bool)(_isNullable = Nullable.GetUnderlyingType(Type) != null));

        private bool? _isTuple = null;
        public bool IsTuple => _isTuple.HasValue ? _isTuple.Value : ((bool)(_isTuple = NonNullableType.Type.FullName.StartsWith(VALUETUPLE_TYPENAME)));

        // We use C#'s definition of an initializable collection, which is any type that implements IEnumerable and has a public Add() method.
        // In our case, we also require that the Add() method has parameters of the same type as the collection's generic parameters.
        private bool? _isInitializableCollection = null;
        public bool IsInitializableCollection => _isInitializableCollection.HasValue ? _isInitializableCollection.Value : ((bool)(_isInitializableCollection = IsInitializableCollectionImpl()));

        private Lazy<MethodInfo?> _addMethod;
        public MethodInfo? AddMethod => _addMethod.Value;

        private bool IsInitializableCollectionImpl()
        {
            var genericParameters = GenericArguments;
            var addMethod = Type.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance, null, genericParameters, null);

            return Type.GetInterfaces().Any(i => i == typeof(IEnumerable)) && addMethod != null;
        }

        private ConstructorInfo[]? _constructors = null;
        public ConstructorInfo[] Constructors => _constructors ?? (_constructors = NonNullableType.Type.GetConstructors());

        public bool IsContainerOfSize(int numParams)
        {
            var constructors = Constructors.Where(cons => cons.GetParameters().Length == numParams);
            return constructors.Count() == 1;
        }

        public bool IsDirectlyConstructable {
            get
            {
                if (Type == typeof(string))
                {
                    return true;
                }

                if (IsTuple)
                {
                    return false;
                }

                var parse = NonNullableType.Type.GetMethod("Parse",
                                BindingFlags.Static | BindingFlags.Public,
                                null,
                                new Type[] { typeof(string) },
                                null);

                if (parse is not null)
                {
                    return true;
                }

                if (NonNullableType.Type.BaseType == typeof(Enum))
                {
                    return true;
                }

                return false;
            }
        }

        protected Type[] GetTupleArgumentsList(Type type)
        {
            var typeArgs = type.GetGenericArguments();

            if (IsTuple && typeArgs.Length == 8)
            {
                return typeArgs.Take(7).Concat(GetTupleArgumentsList(typeArgs[7])).ToArray();
            }
            else
            {
                return typeArgs;
            }
        }

        private const string VALUETUPLE_TYPENAME = "System.ValueTuple`";
        private const string NULLABLE_TYPENAME = "System.Nullable`";
    }
}
