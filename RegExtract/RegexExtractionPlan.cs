using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using static RegExtract.Utils;

namespace RegExtract
{
    internal record RegexExtractionPlan(string Name, List<RegexExtractionPlan> ConstructorGroups, List<RegexExtractionPlan> PropertyGroups, Type Type)
    {
        internal static RegexExtractionPlan CreatePlan<T>(string regex)
        {
            var type = typeof(T);
            new Regex(regex);
            int loc = 0, num = 0;

            if (type.FullName.StartsWith(LIST_TYPENAME) || ArityOfType(type) == 1)
            {
                type = typeof(ValueTuple<>).MakeGenericType(new[] { type });
            }
            return CreatePlan(regex, ref loc, ref num, type);
        }

        static RegexExtractionPlan BindPropertyPlan(string regex, ref int loc, ref int num, Type type, string name)
        {
            if (type.FullName.StartsWith(NULLABLE_TYPENAME))
            {
                type = type.GetGenericArguments().Single();
            }

            var property = type.GetProperty(name);

            if (property is null)
                throw new ArgumentException($"Could not find property for named capture group '{name}'.");

            type = property.PropertyType;

            return CreatePlan(regex, ref loc, ref num, type, name);
        }

        static RegexExtractionPlan BindConstructorPlan(string regex, ref int loc, ref int num, Type type, int paramNum)
        {
            if (type.FullName.StartsWith(NULLABLE_TYPENAME))
            {
                type = type.GetGenericArguments().Single();
            }

            var constructors = type.GetConstructors()
                       .Where(cons => cons.GetParameters().Length != 0);

            if (type.FullName.StartsWith(LIST_TYPENAME))
            {
                type = type.GetGenericArguments().Single();
            }
            else if (type.FullName.StartsWith(VALUETUPLE_TYPENAME))
            {
                type = GetTupleArgumentsList(type)[paramNum];
            }
            else if (constructors?.Count() == 1)
            {
                var constructor = constructors.Single();

                type = constructor.GetParameters()[paramNum].ParameterType;
            }

            return CreatePlan(regex, ref loc, ref num, type);
        }

        static RegexExtractionPlan CreatePlan(string regex, ref int loc, ref int num, Type type, string? name = null)
        {
            string myname = name ?? num.ToString();
            List<RegexExtractionPlan> groups = new();
            List<RegexExtractionPlan> namedgroups = new();

            int charGroupLevels = 0;
            bool escape = false;
            int nameStart = -1;
            int ignoreGroups = 0;
            char openchar = ' ';

            for (; loc < regex.Length; loc++)
            {
                if (escape)
                {
                    escape = false;
                    continue;
                }
                if (nameStart != -1)
                {
                    if ((openchar == '<' && regex[loc] == '>') || (openchar == '\'' && regex[loc] == '\''))
                    {
                        loc++;
                        namedgroups.Add(BindPropertyPlan(regex, ref loc, ref num, type, regex.Substring(nameStart, loc - nameStart - 1)));
                        nameStart = -1;
                        continue;
                    }
                    else if (!char.IsLetterOrDigit(regex[nameStart]) && regex[loc] != '_') throw new Exception("Group Name must be a valid C identifier.");
                }
                if (charGroupLevels > 0)
                {
                    if (regex[loc] == '\\')
                    {
                        escape = true;
                        continue;
                    }
                    if (regex[loc] == '-' && regex[loc + 1] == '[')
                    {
                        loc += 2;
                        if (regex[loc] == '^') loc++;
                        if (regex[loc] == '\\') escape = true;
                        charGroupLevels++;
                        continue;
                    }
                    else if (regex[loc] == ']') charGroupLevels--;
                    continue;
                }

                switch (regex[loc])
                {
                    case '\\':
                        escape = true;
                        break;
                    case '(':
                        if (regex[loc + 1] == '?')
                        {
                            if (regex[loc + 2] != '<' && regex[loc + 2] != '\'')
                            {
                                ignoreGroups++;
                                continue;
                            }
                            else
                            {
                                openchar = regex[loc + 2];
                                loc += 3;
                                nameStart = loc;
                                if (!char.IsLetter(regex[nameStart]) && regex[nameStart] != '_') throw new Exception("Group Name must be a valid C identifier.");
                            }
                        }
                        else
                        {
                            num++;
                            loc++;
                            groups.Add(BindConstructorPlan(regex, ref loc, ref num, type, groups.Count));
                        }
                        break;
                    case ')':
                        if (ignoreGroups > 0)
                        {
                            ignoreGroups--;
                            continue;
                        }
                        else
                        {
                            if (myname == "0") throw new Exception("Too many close parens.");
                            return new RegexExtractionPlan(myname, groups, namedgroups, type);
                        }
                        break;
                    case '[':
                        loc++;
                        if (regex[loc] == '^') loc++;
                        if (regex[loc] == '\\') escape = true;
                        charGroupLevels++;
                        break;
                    default:
                        break;
                }
            }

            if (loc == regex.Length)
            {
                if (myname != "0") throw new Exception("Not enough close parens.");
                if (charGroupLevels > 0) throw new Exception("Unterminated char group.");
            }

            return new RegexExtractionPlan(myname, groups, namedgroups, type);
        }

        internal static int ArityOfType(Type type, bool recursive = false)
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

        static Type[] GetTupleArgumentsList(Type type)
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
