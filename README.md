# RegExtract
Quick and dirty idiomatic C# line parser that emits typed ValueTuples.

[![NuGet](https://img.shields.io/nuget/v/RegExtract.svg?style=flat)](https://www.nuget.org/packages/RegExtract/)

# Table of Contents
- [RegExtract](#regextract)
- [Table of Contents](#table-of-contents)
  - [Usage Examples](#usage-examples)
    - [Date from email header](#date-from-email-header)
    - [List of words](#list-of-words)
    - [Alternation](#alternation)
    - [Parsing fields](#parsing-fields)
    - [Enums and Flags](#enums-and-flags)
  - [History](#history)

## Usage Examples
### Date from email header
```csharp
DateTime date = "Date: Mon, 7 Dec 2020 19:43:24 -0800".Extract<DateTime>(@"Date: (.*)");
```
### List of words
```csharp
List<string> words = "The quick brown fox jumped over the lazy dogs.".Extract<List<string>>(@"(?:(\w+)\W*)+");
```
### Alternation
```csharp
var (n,s) = "str".Extract<(int?,string)>(@"(\d+)|(.*)");
```
### Parsing fields
```csharp
var (n1,y1,e1) = "Hello, earthling, from 2077!".Extract<ParseResult>(@"Hello, (.*), from (?:(\d+)|(.*))!");
var (n2,y2,e2) = "Hello, martian, from earth!".Extract<ParseResult>(@"Hello, (.*), from (?:(\d+)|(.*))!");

record ParseResult(string name, int? year, string loc);
```
### Enums and Flags
```csharp
var mode = "OpenOrCreate".Extract<FileMode>(@".*");
var flags = "Public,Static".Extract<BindingFlags>(".*");
```
## History
This project came about during [day 2 of Advent of Code 2020][1].
The task involved parsing a strings that looked something like:

    1-3 a: abcde
    1-3 b: cdefg
    2-9 c: ccccccccc

[1]: https://adventofcode.com/2020/day/2

From each one, I needed two numbers, a character, and a string.
25 years ago I would have written the following absolutely trivial C code that would take care of parsing it:

```c
int lo, hi; char ch; char pwd[50];

sscanf(line, "%d-%d %c: %s", &lo, &hi, &ch, pwd);
```


It bothered me that the C to parse this was so much simpler than what I would wirte in C#.
For contests, like Advent of Code or Google Code Jam, I usually make extensive use of `.Split()` a la:

```csharp
(int lo, int hi, char ch, string pwd) ParseLine(string line)
{
    var splits = line.Split(" ");
    var nums = splits[0].Split("-").Select(int.Parse).ToArray();
    return (nums[0], nums[1], splits[1][0], splits[2]);
}
```

But that's fiddly to write, and even fiddlier to read.
It's good enough for contest conditions, but leaves lots to be desired for sharing solutions and discussing approaches with other competitors.

Undoubtedly, a regular expression has most of the simplicity of that `sscanf` template from above--you can see what the extra characters are in the template, and you can see what's being extracted:

```csharp
@"(\d+)-(\d+) (.): (.*)"
```

Unfortunately .NET `Regex` `Match`es leave a lot of fiddling after matching to get to the simple types that you want to use for computation:

```csharp
(int lo, int hi, char ch, string pwd) ParseLine(string line)
{
    var match = Regex.Match(line, @"(\d+)-(\d+) (.): (.*)");

    return (int.Parse(match.Groups[1].Value),
            int.Parse(match.Groups[2].Value),
            match.Groups[3].Value[0],
            match.Groups[4].Value);
}
```

So I set out to design the best possible C# syntax that got me somewhere near the expressiveness and simplicity of the `sscanf` example from the 1960s.

Here's where I settled:

```csharp
var (lo, hi, ch, pwd) = line.Extract<(int, int, char, string)>(@"(\d+)-(\d+) (.): (.*)");
```

From there I added support for types with friendly constructors:

```csharp
var (lo, hi, ch, pwd) = line.Extract<template>(@"(\d+)-(\d+) (.): (.*)");

record template(int lo, int hi, char ch, string pwd);
```

And support for extracting named groups to properties:

```csharp
var result = line.Extract<template>(@"(?<lo>\d+)-(?<hi>\d+) (?<ch>.): (?<pwd>.*)");

class template {
    public int lo { get; set; }
    public int hi { get; set; }
    public char ch { get; set; }
    public string pwd { get; set; }
}
```