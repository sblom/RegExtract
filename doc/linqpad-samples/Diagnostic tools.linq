<Query Kind="Statements">
  <Reference Relative="..\..\RegExtract\bin\Debug\netstandard2.1\RegExtract.dll">C:\src\public\RegExtract\RegExtract\bin\Debug\netstandard2.1\RegExtract.dll</Reference>
  <Namespace>RegExtract</Namespace>
</Query>

var plan = ExtractionPlan<((int x, int y), char ch, string pwd)>.CreatePlan(new Regex(@"((\d+)-(\d+)) (.): (.*)"));
var diagnostics = plan.ToString("x");

Util.WithStyle(diagnostics, "font-family:consolas").DumpFixed();