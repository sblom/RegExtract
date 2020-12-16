using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RegExtract
{
    internal record RegexCaptureGroupNode(string name, RegexCaptureGroupNode[] children, string rxText)
    {
        public IEnumerable<RegexCaptureGroupNode> NamedGroups => children.Where(node => !int.TryParse(node.name, out var _));
        public IEnumerable<RegexCaptureGroupNode> NumberedGroups => children.Where(node => int.TryParse(node.name, out var _));
    }

    internal class RegexCaptureGroupTree
    {
        public List<string> Groups { get; private set; } = new() { "0" };

        public Regex Regex { get; private set; }
        private string _regexString;

        public RegexCaptureGroupNode Tree { 
            get
            {
                if (_tree is null)
                {
                    InitializeTree();
                }
                return _tree!;
            }
            private set
            {
                _tree = value;
            }
        }
        private RegexCaptureGroupNode? _tree;

        public RegexCaptureGroupTree(Regex rx)
        {
            Regex = rx;
            _regexString = rx.ToString();
            InitializeTree();
        }

        public RegexCaptureGroupTree(string rx) : this(new Regex(rx))
        {
            _regexString = rx;
        }

        private void InitializeTree()
        {
            int loc = 0, num = 0;
            Tree = BuildCaptureGroupTree(ref loc, ref num, 0);
        }

        private RegexCaptureGroupNode BuildCaptureGroupTree(ref int loc, ref int num, int start, string? name = null)
        {
            string myname = name ?? num.ToString();
            List<RegexCaptureGroupNode> children = new();

            int charGroupLevels = 0;
            bool escape = false;
            int nameStart = -1;
            int ignoreGroups = 0;
            char openchar = ' ';
            int groupStart = 0;

            for (; loc < _regexString.Length; loc++)
            {
                if (escape)
                {
                    escape = false;
                    continue;
                }
                if (nameStart != -1)
                {
                    if ((openchar == '<' && _regexString[loc] == '>') || (openchar == '\'' && _regexString[loc] == '\''))
                    {
                        loc++;
                        var parsedName = _regexString.Substring(nameStart, loc - nameStart - 1);
                        Groups.Add(parsedName);
                        children.Add(BuildCaptureGroupTree(ref loc, ref num, groupStart, _regexString.Substring(nameStart, loc - nameStart - 1)));
                        nameStart = -1;
                        continue;
                    }
                    else if (!char.IsLetterOrDigit(_regexString[nameStart]) && _regexString[loc] != '_') throw new Exception("Group Name must be a valid C identifier.");
                }
                if (charGroupLevels > 0)
                {
                    if (_regexString[loc] == '\\')
                    {
                        escape = true;
                        continue;
                    }
                    if (_regexString[loc] == '-' && _regexString[loc + 1] == '[')
                    {
                        loc += 2;
                        if (_regexString[loc] == '^') loc++;
                        if (_regexString[loc] == '\\') escape = true;
                        charGroupLevels++;
                        continue;
                    }
                    else if (_regexString[loc] == ']') charGroupLevels--;
                    continue;
                }

                switch (_regexString[loc])
                {
                    case '\\':
                        escape = true;
                        break;
                    case '(':
                        groupStart = loc;
                        if (_regexString[loc + 1] == '?')
                        {
                            if (_regexString[loc + 2] != '<' && _regexString[loc + 2] != '\'')
                            {
                                ignoreGroups++;
                                continue;
                            }
                            else
                            {
                                openchar = _regexString[loc + 2];
                                loc += 3;
                                nameStart = loc;
                                if (!char.IsLetter(_regexString[nameStart]) && _regexString[nameStart] != '_') throw new Exception("Group Name must be a valid C identifier.");
                            }
                        }
                        else
                        {
                            num++;
                            loc++;
                            Groups.Add(num.ToString());
                            children.Add(BuildCaptureGroupTree(ref loc, ref num, groupStart));
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
                            return new RegexCaptureGroupNode(myname, children.ToArray(), _regexString.Substring(start, loc - start + 1));
                        }
                    case '[':
                        loc++;
                        if (_regexString[loc] == '^') loc++;
                        if (_regexString[loc] == '\\') escape = true;
                        charGroupLevels++;
                        break;
                    default:
                        break;
                }
            }

            // TODO: These should probably be asserts, because Regex has validated everything after constructor is complete.
            if (loc > _regexString.Length) throw new Exception("Parser over-ran end of regex string.");
            if (myname != "0") throw new Exception("Not enough close parens.");
            if (charGroupLevels > 0) throw new Exception("Unterminated char group.");

            Groups = Groups.OrderBy(name => int.TryParse(name, out var _) ? 0 : 1).ToList();
            Debug.Assert(Groups.Zip(Regex.GetGroupNames(), (a,b) => a == b).All(b => b), "Group List doesn't match Regex.GetGroupNames()");

            return new RegexCaptureGroupNode(myname, children.ToArray(), _regexString.Substring(start, loc - start - 1));
        }
    }
}
