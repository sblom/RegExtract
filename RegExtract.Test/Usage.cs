using System;

using Xunit;

using System.Text.RegularExpressions;
using System.Collections.Generic;
using Xunit.Abstractions;

namespace RegExtract.Test
{
    public class Usage
    {
        private readonly ITestOutputHelper output;

        public Usage(ITestOutputHelper output)
        {
            this.output = output;
        }

        const string data = "123456789";
        const string pattern = "(.)(.)(.)(.)(.)(.)(.)(.)(.)";
        const string pattern_nested = "(((.)(.)(.)(.)(.)(.)(.)(.)(.)))";
        const string pattern_named = "(?<n>(?<s>(?<a>.)(?<b>.)(?<c>.)(?<d>.)(?<e>.)(?<f>.)(?<g>.)(?<h>.)(?<i>.)))";

        [Fact]
        public void a001()
        {
            var str = ExtractionPlan<List<(char, char)>>.CreatePlan(new Regex(@"((\w)(\w))+")).ToString("x");
            output.WriteLine(str);
        }

        [Fact]
        public void a002()
        {
            var str = ExtractionPlan<List<int>>.CreatePlan(new Regex(@"((\d+) ?)+")).ToString("x");
            output.WriteLine(str);
        }

        record game(int id, List<draw> draws);
        record draw(List<(int count, string color)> colors);

        [Fact]
        public void a003()
        {
            var plan = ExtractionPlan<game>.CreatePlan(new Regex(@"Game (\d+): (((\d+) (\w+),? ?)+;? ?)+"));
            var str = plan.ToString("x");
            output.WriteLine(str);

            var result = plan.Extract("Game 31: 9 blue, 6 red, 7 green; 20 red, 1 green, 15 blue; 6 blue, 7 green, 17 red; 2 blue, 3 green, 6 red; 1 red, 3 blue, 2 green; 5 green, 18 red, 6 blue");
        }

        [Fact]
        public void a004()
        {
            var plan = ExtractionPlan<List<(char, int)>>.CreatePlan(new Regex(@"(([RL])(\d+),? ?)+"));
            var str = plan.ToString("x");
            output.WriteLine(str);

            var result = plan.Extract("R8, R4, L4, R8");
        }


        [Fact]
        public void a005()
        {
            var plan = ExtractionPlan<Dictionary<string, (string left, string right)>>.CreatePlan(new Regex(@"((...) = \(((...), (...))\);? ?)+"));
            var str = plan.ToString("x");
            output.WriteLine(str);

            var result = plan.Extract(@"AAA = (BBB, CCC); BBB = (DDD, EEE)");
        }

        [Fact]
        public void a006()
        {
            var str = ExtractionPlan<List<int>>.CreatePlan(new Regex(@"((\d+) ?)+")).ToString("x");
            output.WriteLine(str);
        }

        [Fact]
        public void a007()
        {
            var str = ExtractionPlan<List<int>>.CreatePlan(new Regex(@"(?:(\d+) ?)+")).ToString("x");
            output.WriteLine(str);
        }

        [Fact]
        public void can_parse_lookbehind()
        {
            data.Extract<string>(@"(?<=(12))");
        }

        [Fact]
        public void can_extract_to_tuple()
        {
            var (a, b, c, d, e, f, g, h, i) = data.Extract<(int, char, string, int, char, string, int, char, string)>(pattern);

            Assert.IsType<int>(a);
            Assert.IsType<char>(b);
            Assert.IsType<string>(c);
            Assert.IsType<int>(d);
            Assert.IsType<char>(e);
            Assert.IsType<string>(f);
            Assert.IsType<int>(g);
            Assert.IsType<char>(h);
            Assert.IsType<string>(i);

            Assert.Equal(1, a);
            Assert.Equal('2', b);
            Assert.Equal("3", c);
            Assert.Equal(4, d);
            Assert.Equal('5', e);
            Assert.Equal("6", f);
            Assert.Equal(7, g);
            Assert.Equal('8', h);
            Assert.Equal("9", i);
        }

        record PositionalRecord(int a, char b, string c, int d, char e, string f, int g, char h, string i);

        [Fact]
        public void can_extract_to_positional_record()
        {
            PositionalRecord? record = data.Extract<PositionalRecord>(pattern);

            Assert.NotNull(record);

            var (a, b, c, d, e, f, g, h, i) = record!;

            Assert.IsType<int>(a);
            Assert.IsType<char>(b);
            Assert.IsType<string>(c);
            Assert.IsType<int>(d);
            Assert.IsType<char>(e);
            Assert.IsType<string>(f);
            Assert.IsType<int>(g);
            Assert.IsType<char>(h);
            Assert.IsType<string>(i);

            Assert.Equal(1, a);
            Assert.Equal('2', b);
            Assert.Equal("3", c);
            Assert.Equal(4, d);
            Assert.Equal('5', e);
            Assert.Equal("6", f);
            Assert.Equal(7, g);
            Assert.Equal('8', h);
            Assert.Equal("9", i);
        }

        [Fact]
        public void fails_when_positional_record_is_wrong_arity()
        {
            Assert.Throws<ArgumentException>(() => data.Extract<PositionalRecord>(pattern_nested));
        }

        record PropertiesRecord
        {
            public string? s { get; init; }
            public long? n { get; init; }
            public int? a { get; init; }
            public char? b { get; init; }
            public string? c { get; init; }
            public int? d { get; init; }
            public char? e { get; init; }
            public string? f { get; init; }
            public int? g { get; init; }
            public char? h { get; init; }
            public string? i { get; init; }
        }

        // Don't currently handle nested named captures, and I'm not sure we ever will.
        //[Fact]
        //public void can_extract_named_capture_groups_to_properties()
        //{
        //    PropertiesRecord? record = data.Extract<PropertiesRecord>(pattern_named);
        //}

        record Passport
        {
            public int? byr { get; set; }
            public int? iyr { get; set; }
            public int? eyr { get; set; }
            public string? hgt { get; set; }
            public string? hcl { get; set; }
            public string? ecl { get; set; }
            public string? pid { get; set; }
        }

        [Fact]
        public void can_extract_mondo_conditional_regex()
        {
            var mondoString = @"
^(?:\b
(?: (?:byr: (?:(?<byr>19[2-9][0-9]|200[0-2])                           |.*?) )
|    (?:iyr: (?:(?<iyr>20(?:1[0-9]|20))                                   |.*?) )
|    (?:eyr: (?:(?<eyr>20(?:2[0-9]|30))                                   |.*?) )
|    (?:hgt: (?:(?<hgt>(?:(?:59|6[0-9]|7[0-6])in)|(?:1(?:[5-8][0-9]|9[0-3])cm)) |.*?) )
|    (?:hcl: (?:(?<hcl>\#[0-9a-f]{6})                                   |.*?) )
|    (?:ecl: (?:(?<ecl>amb|blu|brn|gry|grn|hzl|oth)                     |.*?) )
|    (?:pid: (?:(?<pid>[0-9]{9})                                        |.*?) )
|    (?:cid: (?:.*?)                                                          )
)
\b\s*)+
$
";
            var mondo = new Regex(mondoString, RegexOptions.IgnorePatternWhitespace);

            var result = "hgt:61in iyr:2014 pid:916315544 hcl:#733820 ecl:oth".Extract<Passport>(mondoString,RegexOptions.IgnorePatternWhitespace);

            //TODO: this was the only test using the `this Match` extension for Extract. Should re-add one.
        }

        record Container
        {
            public string? container { get; init; }
            public List<int?>? count { get; init; }
            public List<string?>? bag { get; init; }
            public string? none { get; init; }
        }

        [Fact]
        public void can_extract_capture_collections_to_lists()
        {
            var line = "faded yellow bags contain 4 mirrored fuchsia bags, 4 dotted indigo bags, 3 faded orange bags, 5 plaid crimson bags.";
            var regex = @"^(?<container>.+) bags contain(?: (?<none>no more bags\.)| (?<count>\d+) (?<bag>[^,.]*) bag[s]?[,.])+$";

            var output = line.Extract<Container>(regex);
        }

        [Fact]
        public void can_extract_single_item()
        {
            var output = "asdf".Extract<string>("(.*)");
            Assert.Equal("asdf", output);

            var n = "2023".Extract<int>(@"(\d+)");
            Assert.Equal(2023, n);
        }

        [Fact]
        public void can_extract_multimatch_to_list()
        {
            var result = "123 456 789".Extract<List<int>> (@"(?:(\d+) ?)+");
        }

        [Fact]
        public void can_extract_multimatch_to_hashset()
        {
            var result = "123 456 789".Extract<HashSet<int>>(@"(?:(\d+) ?)+");
        }

        [Fact]
        public void can_extract_alternation_to_tuple()
        {
            var result = "asdf".Extract<(int?, string)>(@"(\d+)|(.*)");
        }

        record Alternation(int? n, string s);

        record NamedAlternation
        {
            public int? n { get; init; }
            public string? s { get; init; }
        }

        [Fact]
        public void can_extract_alternation_to_record()
        {
            var result = "asdf".Extract<Alternation>(@"(\d+)|(.*)");
            var result_named = "asdf".Extract<NamedAlternation>(@"(?<n>\d+)|(?<s>.*)");
        }

        [Fact]
        public void can_extract_enum()
        {
            var result = "Asynchronous,Encrypted".Extract<System.IO.FileOptions>(@".*");
        }

        record WithTemplate(string op, int arg)
        {
            public const string REGEXTRACT_REGEX_PATTERN = @"(\S+) ([+-]?\d+)";
            public const RegexOptions REGEXTRACT_REGEX_OPTIONS = RegexOptions.None;
        }

        [Fact]
        public void can_extract_with_template()
        {
            var result = "acc +7".Extract<WithTemplate>();
        }

        const RegexOptions opts = RegexOptions.IgnoreCase|RegexOptions.Multiline;

        [Fact]
        public void can_extract_to_string_constructor()
        {
            var result = "https://www.google.com/ 12345".Extract<(Uri,int)>(@"(.*) (\d+)");
        }

        [Fact]
        public void regex_does_not_match()
        {
            Assert.Throws<ArgumentException>(()=>"https://www.google.com/".Extract<Uri>(@"\d+"));
        }

        record bounds(int lo, int hi);

        [Fact]
        public void nested_extraction()
        {
            var result = "2-12 c: abcdefg".Extract<(bounds, char, string)>(@"((\d+)-(\d+)) (.): (.*)");
        }

        [Fact]
        public void nested_extraction_of_list()
        {
            var result = "The quick brown fox jumps over the lazy dog.".Extract<List<List<char>>>(@"(?:((\w)+) ?)+");
        }

        [Fact]
        public void nested_extraction_of_bags()
        {
            var line = "faded yellow bags contain 4 mirrored fuchsia bags, 4 dotted indigo bags, 3 faded orange bags, 5 plaid crimson bags.";
            var regex = @"^(.+) bags contain(?: (no more bags\.)| ((\d+) ([^,.]*)) bag[s]?[,.])+$";

            var output = line.Extract<(string,string,List<(int,string)>)>(regex);
        }

        [Fact]
        public void extraction_plan()
        {
            var regex = new Regex(@"((\d+)-(\d+)) (.): (.*)");
            var match = regex.Match("2-12 c: abcdefg");
            var plan = ExtractionPlan<((int, int), char, string)>.CreatePlan(regex);
            var result = plan.Extract(match);
        }

        [Fact]
        public void extraction_plan_to_long_tuple()
        {
            var regex = new Regex(pattern);
            var match = Regex.Match(data, pattern);
            var plan = ExtractionPlan<(int?, char, string, int, char, string, int, char, string)>.CreatePlan(regex);

            var (a, b, c, d, e, f, g, h, i) = plan.Extract(match);

            Assert.IsType<int>(a);
            Assert.IsType<char>(b);
            Assert.IsType<string>(c);
            Assert.IsType<int>(d);
            Assert.IsType<char>(e);
            Assert.IsType<string>(f);
            Assert.IsType<int>(g);
            Assert.IsType<char>(h);
            Assert.IsType<string>(i);

                 var (verb, year) =
              "Party like it's 1999"
               .Extract<(string, int)>
            (@"(\w+) like it's (\d)+");

            Assert.Equal(1, a);
            Assert.Equal('2', b);
            Assert.Equal("3", c);
            Assert.Equal(4, d);
            Assert.Equal('5', e);
            Assert.Equal("6", f);
            Assert.Equal(7, g);
            Assert.Equal('8', h);
            Assert.Equal("9", i);
        }
        
        [Fact]
        public void debug()
        {
            //var data = "faded yellow bags contain 4 mirrored fuchsia bags, 4 dotted indigo bags, 3 faded orange bags, 5 plaid crimson bags.";
            //var plan = RegexExtractionPlan.CreatePlan<(string, string, List<(int?, string)?>)>(@"^(.+) bags contain(?: (no other bags)\.| ((\d+) (.*?)) bags?[,.])+$");
            //var result = plan.Execute(Regex.Match(data, @"^(.+) bags contain(?: (no other bags)\.| ((\d+) (.*?)) bags?[,.])+$"));

            Regex rx;
            var plan = ExtractionPlan<bagdescription>.CreatePlan(rx = new Regex(@"^(?<name>.+) bags contain(?: (?<none>no other bags)\.| (?<contents>(\d+) (.*?)) bags?[,.])+$"));
            var result = plan.Extract(rx.Match("faded yellow bags contain 4 mirrored fuchsia bags, 4 dotted indigo bags, 3 faded orange bags, 5 plaid crimson bags."));
        }

        record bagdescription
        {
            public string? name { get; init; }
            public string? none { get; init; }
            public List<includedbags>? contents { get; init; }
        }
        record includedbags(int? num, string name);

        [Fact]
        public void CreateTreePlan()
        {
            var regex = new Regex(@"((\d+)-(\d+)) (.): (.*)");
            var plan = ExtractionPlan<((int?, int?)?, char, string)?>.CreatePlan(regex);
            object? result = plan.Extract(regex.Match("2-12 c: abcdefgji"));

            regex = new Regex(@"(?:((\w)+) ?)+");
            var plan2 = ExtractionPlan<List<List<char>>>.CreatePlan(regex);

            result = plan2.Extract(regex.Match("The quick brown fox jumps over the lazy dog"));
        }

        [Fact]
        public void can_create_polymorphic_parse_plan()
        {
            var plan = ExtractionPlan<instr>.CreatePlan(new Regex(@"(.*)"));
            var results = plan.Extract("mask = lkjasdf");
        }

        record instr()
        {
            public static instr Parse(string str)
            {
                if (str.StartsWith("mask"))
                {
                    return str.Extract<maskinstr>(@"mask = (.+)")!;
                }
                else
                {
                    return str.Extract<meminstr>(@"mem\[(\d+)] = (\d+)")!;
                }
            }
        }
        record maskinstr(string mask) : instr;
        record meminstr(long loc, long val) : instr;
    }
}

// This is here to enable use of record types in .NET 3.1.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
