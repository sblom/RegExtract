<Query Kind="Statements">
  <Reference Relative="..\..\RegExtract\bin\Debug\netstandard2.1\RegExtract.dll">C:\src\public\RegExtract\RegExtract\bin\Debug\netstandard2.1\RegExtract.dll</Reference>
  <Namespace>RegExtract</Namespace>
</Query>

// You can nest parentheses in the Regex. The order of the types in your typle
// should correspond to the open parenthesis of each capture group.
(Uri uri, string protocol, string host, int port, string path) nested =
    "https://nuget.org:443/packages/RegExtract"
        .Extract<(Uri, string, string, int, string)>(@"((\S+)://(\S+):(\d+)(\S*))");

nested.Dump("Nested parens in Regex");
