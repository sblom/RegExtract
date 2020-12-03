<Query Kind="Statements">
  <Reference Relative="..\..\RegExtract\bin\Debug\netstandard2.1\RegExtract.dll">C:\src\personal\RegExtract\RegExtract\bin\Debug\netstandard2.1\RegExtract.dll</Reference>
  <Namespace>RegExtract</Namespace>
</Query>

var strings = 
    "https://nuget.org:443/packages/RegExtract"
        .Extract<(string,string,string,string)>(@"(\S+)://(\S+):(\d+)(\S*)");

strings.Dump("Strings only");

// You can use any type <T> that has a public static T.Parse(string) method,
// or a public T(string) constructor.
(string protocol, string host, int port, string path) types =
    "https://nuget.org:443/packages/RegExtract"
        .Extract<(string, string, int, string)>(@"(\S+)://(\S+):(\d+)(\S*)");

types.Dump("Types other than string");

// You can nest parentheses in the Regex. The order of the types in your typle
// should correspond to the open parenthesis of each capture group.
(Uri uri, string protocol, string host, int port, string path) nested =
    "https://nuget.org:443/packages/RegExtract"
        .Extract<(Uri, string, string, int, string)>(@"((\S+)://(\S+):(\d+)(\S*))");
        
nested.Dump("Nested parens in Regex");
nested.uri.Dump("Individual item");

// Instead of a tuple, you can use any type with a single non-default public constructor.
// The most useful examples of this will probably be C# 9's record types.
UrlRecord url = "https://nuget.org:443/packages/RegExtract"
    .Extract<UrlRecord>(@"((\S+)://(\S+):(\d+)(\S*))");

url.Dump("Record type");

record UrlRecord(Uri uri, string protocol, string host, int port, string path);