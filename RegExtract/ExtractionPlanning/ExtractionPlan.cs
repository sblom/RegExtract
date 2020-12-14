using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace RegExtract
{
    public abstract class ExtractionPlan<T>
    {
        public ExtractionPlanNode Plan { get; protected set; }

        protected ExtractionPlan() { }

        abstract internal void InitializePlan(Regex regex);

        public T Extract(Match match)
        {
            return (T)Plan.Execute(match);
        }

        static public ExtractionPlan<T> CreatePlan(Regex regex, RegExtractOptions reOptions= RegExtractOptions.None)
        {
            ExtractionPlan<T> plan = new FlatExtractionPlan<T>();
            plan.InitializePlan(regex);

            return plan;
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

        protected bool IsBottomType(Type type)
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

            if (type.FullName.StartsWith("System.Nullable`"))
            {
                return ArityOfType(type.GetGenericArguments().Single());
            }
            else if (type.FullName.StartsWith("System.Collections.Generic.List`"))
            {
                var subtype = type.GetGenericArguments().Single();
                if (subtype.FullName.StartsWith("System.Collections.Generic.List`"))
                {
                    return 1 + ArityOfType(subtype);
                }
                else
                {
                    return ArityOfType(subtype);
                }
            }
            else if (type.FullName.StartsWith("System.ValueTuple`"))
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

            if (type.FullName.StartsWith(VALUETUPLE_TYPENAME) && typeArgs.Length == 8)
            {
                return typeArgs.Take(7).Concat(GetTupleArgumentsList(typeArgs[7])).ToArray();
            }
            else
            {
                return typeArgs;
            }
        }

    }
}
