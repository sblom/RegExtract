using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RegExtract.RegexTools
{
    internal record RegexCaptureGroupNode(string name, RegexCaptureGroupNode[] children, ((int start, int length) range, string text) substring)
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
                            // ? may be followed by lookbehind (which starts out looking like a <> named group) or something that's clearly not a named group
                            if ((_regexString[loc + 2] == '<' && (_regexString[loc + 3] == '=' || _regexString[loc + 3] == '!')) || (_regexString[loc + 2] != '<' && _regexString[loc + 2] != '\''))
                            {
                                ignoreGroups++;
                                continue;
                            }
                            // otherwise, it's a named group
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
                            return new RegexCaptureGroupNode(myname, children.ToArray(), ((start, loc - start + 1),_regexString.Substring(start, loc - start + 1)));
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

            return new RegexCaptureGroupNode(myname, children.ToArray(), ((start, loc - start), _regexString.Substring(start, loc - start)));
        }

        string IntToSubscripts(int i)
        {
            List<char> digits = new();

            do
            {
                digits.Add((i % 10) switch
                {
                    0 => '₀',
                    1 => '₁',
                    2 => '₂',
                    3 => '₃',
                    4 => '₄',
                    5 => '₅',
                    6 => '₆',
                    7 => '₇',
                    8 => '₈',
                    9 => '₉',
                    _ => throw new InvalidOperationException("That's impossible!")
                });
                i /= 10;
            } while (i > 0);

            digits.Reverse();
            return string.Join("", digits);
        }

        public string TreeViz() => String.Join("\n",TreeViz(Tree));

        string[] TreeViz(RegexCaptureGroupNode tree)
        {
            var line = tree.substring.text;

            var tag = int.TryParse(tree.name, out var num) ? IntToSubscripts(num) : "";

            char[] pad;
            string[] results;

            if (!tree.children.Any())
            {
                var solo = $"{line}{tag}";
                results = new[] { solo };

                return results;
            }
            else
            {
                var left = tree.substring.range.start;
                var right = tree.substring.range.start + tree.substring.range.length;

                var blocks = tree.children.Select(child => TreeViz(child)).ToArray();
                var longestblock = blocks.Max(block => block.Length);

                for (int i = 0; i < blocks.Length; i++)
                {
                    if (blocks[i].Length != longestblock)
                    {
                        var blockline = new char[blocks[i][0].Length];
                        for (int j = 0; j < blockline.Length; j++) blockline[j] = ' ';

                        var newblock = new string[longestblock];
                        for (int j = 0; j < newblock.Length; j++)
                        {
                            if (j < blocks[i].Length) newblock[j] = blocks[i][j];
                            else newblock[j] = string.Join("", blockline);
                        }

                        blocks[i] = newblock;
                    }
                }

                var widths = blocks.Select(block => block[0].Length);

                results = new string[blocks[0].Length];

                pad = new char[tree.children[0].substring.range.start - left];
                for (int j = 0; j < pad.Length; j++) pad[j] = ' ';

                for (int i = 0; i < results.Length; i++)
                {
                    results[i] = string.Join("", pad);
                }

                for (int i = 0; i < blocks.Length; i++)
                {
                    var nextstart = i == blocks.Length - 1 ? right : tree.children[i + 1].substring.range.start;
                    pad = new char[nextstart - (tree.children[i].substring.range.start + tree.children[i].substring.range.length)];
                    for (int j = 0; j < pad.Length; j++) pad[j] = ' ';

                    for (int j = 0; j < blocks[0].Length; j++)
                    {
                        results[j] += blocks[i][j];
                        results[j] += string.Join("", pad);
                    }
                }

                var topline = tree.substring.text.Substring(0, tree.children[0].substring.range.start - left);
                for (int i = 0; i < blocks.Length; i++)
                {
                    pad = new char[blocks[i][0].Length];
                    for (int j = 0; j < pad.Length; j++) pad[j] = '-';
                    pad[0] = pad[pad.Length - 1] = '↓';
                    topline += string.Join("", pad);
                    var subleft = tree.children[i].substring.range.start + tree.children[i].substring.range.length;
                    topline += tree.substring.text.Substring(subleft - left, i == blocks.Length - 1 ? right - subleft : tree.children[i + 1].substring.range.start - subleft);
                }

                pad = new char[tag.Length];
                for (int j = 0; j < pad.Length; j++) pad[j] = ' ';

                results = new[] { $"{topline}{tag}" }.Concat(results.Select(result => result + string.Join("", pad))).ToArray();

                return results;
            }
        }
    }
}
