<Query Kind="Statements">
  <Reference Relative="..\..\RegExtract\bin\Debug\netstandard2.1\RegExtract.dll">C:\src\public\RegExtract\RegExtract\bin\Debug\netstandard2.1\RegExtract.dll</Reference>
  <Namespace>RegExtract</Namespace>
</Query>

// Instead of a tuple, you can use any type with a single non-default public constructor.
// The most useful examples of this will probably be C# 9's record types.
UrlPositionalRecord urlPositional = "https://nuget.org:443/packages/RegExtract"
    .Extract<UrlPositionalRecord>
      (@"((\S+)://(\S+):(\d+)(\S*))");

urlPositional.Dump("Record type (positional)");

// Instead of a tuple, you can use any type with a single non-default public constructor.
// The most useful examples of this will probably be C# 9's record types.
UrlRecord urlProperties = "https://nuget.org:443/packages/RegExtract"
      .Extract<UrlRecord>(@"(?<protocol>\S+)://(?<host>\S+):(?<port>\d+)(?<path>\S*)");

urlProperties.Dump("Record type (properties)");

record UrlPositionalRecord(Uri uri, string protocol, string host, int port, string path);

record UrlRecord
{
    public string protocol { get; init; }
    public string host { get; init; }
    public int port { get; init; }
    public string path { get; init; }
}
