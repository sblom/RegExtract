<Query Kind="Statements">
  <Reference Relative="..\RegExtract\bin\Debug\netstandard2.1\RegExtract.dll">C:\src\public\RegExtract\RegExtract\bin\Debug\netstandard2.1\RegExtract.dll</Reference>
  <Namespace>RegExtract</Namespace>
</Query>

//"2-12 c: abcdefghi".ShowExtractionPlan<((int,int), char, string)>(@"((\d+)-(\d+)) (.): (.*)")
//"The quick brown fox jumps over the lazy dog".ShowExtractionPlan<List<List<char>>>(@"(?:((\w)+) ?)+")
//"2-12 18-3 10-5".ShowExtractionPlan<List<(int,int)>>(@"((\d+)-(\d+) ?)+")
//"faded yellow bags contain 4 mirrored fuchsia bags, 4 dotted indigo bags, 3 faded orange bags, 5 plaid crimson bags.".ShowExtractionPlan<(string,string,string,List<int>,List<string>)>(@"^(.+) bags contain(?: (no other bags)\.| ((\d+) (.*?)) bags?[,.])+$").Dump();
//"faded yellow bags contain 4 mirrored fuchsia bags, 4 dotted indigo bags, 3 faded orange bags, 5 plaid crimson bags.".ShowExtractionPlan<(string,string,List<(int,string)>)>(@"^(.+) bags contain(?: (no other bags)\.| ((\d+) (.*?)) bags?[,.])+$").Dump();

//"2-12 c: abcdefghi".ShowExtractionPlan<((int, int), char?, string)>(@"((\d+)-(\d+)) (.): (.*)").Dump();

//"faded yellow bags contain 4 mirrored fuchsia bags, 4 dotted indigo bags, 3 faded orange bags, 5 plaid crimson bags.".Extr<(string,string,string,List<int>,List<string>)>(@"^(.+) bags contain(?: (no other bags)\.| ((\d+) (.*?)) bags?[,.])+$")

//RegexExtractionPlan.CreatePlan<(string, string, List<(int?, string)?>)>(@"^(.+) bags contain(?: (no other bags)\.| ((\d+) (.*?)) bags?[,.])+$").Dump();
//var plan = RegexExtractionPlan.CreatePlan<List<List<char>>>(@"((\w)+ ?)+").Dump();
//plan.Execute(Regex.Match("The quick brown fox jumps over the lazy dog",@"(?:((\w)+) ?)+").Dump()).Dump();

RegexExtractionPlan.CreatePlan<(long, string, int, char, string, int, char, string, int, char, string)>(@"(((.)(.)(.)(.)(.)(.)(.)(.)(.)))").Dump();

record bound(int lo, int hi);
record rule(string range, int lo, int hi, char ch, string pwd);