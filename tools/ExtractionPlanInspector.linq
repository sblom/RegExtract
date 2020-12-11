<Query Kind="Expression">
  <Reference Relative="..\RegExtract\bin\Debug\netstandard2.1\RegExtract.dll">C:\src\public\RegExtract\RegExtract\bin\Debug\netstandard2.1\RegExtract.dll</Reference>
  <Namespace>RegExtract</Namespace>
</Query>

"2-12 c: abcdefghi".ShowExtractionPlan<((int,int), char, string)>(@"((\d+)-(\d+)) (.): (.*)")
