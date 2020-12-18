<Query Kind="Statements">
  <Reference Relative="..\..\RegExtract\bin\Debug\netstandard2.1\RegExtract.dll">C:\src\public\RegExtract\RegExtract\bin\Debug\netstandard2.1\RegExtract.dll</Reference>
  <Namespace>RegExtract</Namespace>
</Query>

"Hello, world!"
    .Extract<string>(@"Hello, (\w+)!")
    .Dump("Simple extraction");

"Party like it's 1999!"
    .Extract<(string verb, int year)>(@"(\w+) like it's (\d+)!")
    .Dump("Extract multiple captures");
