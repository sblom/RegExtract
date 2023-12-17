# RegExtract
Quick and dirty idiomatic C# line parser that extracts text into practical data types.

[![dotnet](https://github.com/sblom/RegExtract/workflows/dotnet/badge.svg)](https://github.com/sblom/RegExtract/actions)
[![NuGet](https://img.shields.io/nuget/v/RegExtract.svg)](https://www.nuget.org/packages/RegExtract/)
[![Downloads](https://img.shields.io/nuget/dt/RegExtract.svg)](https://www.nuget.org/packages/RegExtract/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Table of Contents
- [RegExtract](#regextract)
  - [Table of Contents](#table-of-contents)
- [Using RegExtract](#using-regextract)
  - [Basic extraction to `ValueTuple`](#basic-extraction-to-valuetuple)
  - [Extracting to collections such as `List<T>`](#extracting-to-collections-such-as-listt)
  - [Nullable types](#nullable-types)
  - [Nesting types](#nesting-types)
  - [Extracting from multiple input strings (`IEnumerable<string>`)](#extracting-from-multiple-input-strings-ienumerablestring)
  - [`record`s and other compound types](#records-and-other-compound-types)
  - [Other supported types](#other-supported-types)
  - [Extracting named capture groups to properties](#extracting-named-capture-groups-to-properties)
- [Performance and troubleshooting](#performance-and-troubleshooting)
  - [Creating a re-usable `ExtractionPlan`](#creating-a-re-usable-extractionplan)
  - [Inspecting an extraction plan](#inspecting-an-extraction-plan)
- [Advanced examples](#advanced-examples)
  - [Date from email header](#date-from-email-header)
  - [List of words](#list-of-words)
  - [Alternation](#alternation)
  - [Parsing fields](#parsing-fields)
  - [Enums and Flags](#enums-and-flags)
- [Regular Expression reference](#regular-expression-reference)
- [History](#history)

# Using RegExtract

## Basic extraction to `ValueTuple`

Let's say you have a string `2-10 c: abcdefghi`, consisting of a two `int`s separated by a dash (-),
a `char` followed by a colon (:), and a `string`.

You could use the regular expression `@"(\d+)-(\d+) (.): (.*)"` to extract that into a tuple
`(int min, int max, char ch, string str)`. Or you could use `@"((\d+)-(\d+)) (.): (.*)"` to extract into a nested
tuple `((int min, int max) range, char ch, string str)`. 

> [!TIP]
> If you need a primer on helpful regular expression syntax, see the
> [Regular Expression Examples](#regular-expression-examples) section below.

In C# code, those two examples would look like:

```cs
using RegExtract;

var input = "2-10 c: abcdefghi";

var flat_tuple   = input.Extract<( int min, int max,        char ch, string str)>(@"(\d+)-(\d+) (.): (.*)");
var nested_tuple = input.Extract<((int min, int max) range, char ch, string str)>(@"((\d+)-(\d+)) (.): (.*)");
```

> [!NOTE]
> The nesting of your capture groups (parts wrapped in `()`) in your regular expression must match the nesting
> of your type hierarchy.

## Extracting to collections such as `List<T>`

In addition to support for arbitrarily long `ValueTuple`s as demonstrated above, any collection type that is
supported by  [C#'s Collection Initializer syntax](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/object-and-collection-initializers#collection-initializers)
(commonly `List<>`, `HashSet<>`, and `Dictionary<,>`).

> [!TIP]
> C# collection initializers work with any *collection type that implements IEnumerable and has Add with the 
> appropriate signature*.

To extract a list, you should include a capture group in your regular expression that repeats. For example, to break
a sentence up into individual words, you can do something like:

```cs
using RegExtract;

var input = "The quick brown fox jumps over the lazy dog.";

var words_with_trailing_spaces = input.Extract<List<string>>(@"(\w+ ?)+");
var words_without_spaces       = input.Extract<List<string>>(@"((\w+) ?)+");
```

Notice in the first example (`words_with_trailing_spaces`), there is only one capture group, and everything inside it 
is treated as part of the match. As a result, the strings in the List include trailing spaces (except for "dog", which stopped matching before the trailing period (.)).

In the second example (`words_without_spaces`), an optional final set of parens was included immediately around `\w+`. 
As a result, the strings in the second list will only include the words themselves without trailing spaces.

> [!NOTE]
> As illustrated by the `words_without_spaces` example, you can always optionally include one final capture group
> inside any **Collection** type in the type hierarchy to which you are extracting. This has the effect of only 
> capturing a substring from within each repeated match.

## Nullable types

## Nesting types

You'll frequently find that you need to nest a **compound type** (such as a tuple) inside a **collection** or that you need to nest a **collection** inside a **compound type**.

## Extracting from multiple input strings (`IEnumerable<string>`)

## `record`s and other compound types

You can build almost anything you need using `ValueTuple`s and `List<>`s, and for simple, ad hoc scenarios that's
often where I begin and end.

However, when it comes time to extract inputs to more richly modeled types, you'll use RegExtract's support for types 
such as `record`s that have a single obvious constructor (some might say a [primary constructor!](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/instance-constructors#primary-constructors)) 
with the number of parameters corresponding to the number of capture groups in your regular expression. (Strictly 
speaking, it doesn't have to be a record, and you don't have to use primary constructor syntax&mdash;absolutely any 
type with a constructor of the right shape is fine.)

## Other supported types

In addition to tuples and collections as discussed above, *all of .NET's primitive types* are supported, as well as
`Enum`, any type that implements a static `.Parse(string)` method, and, as a last resort, any type with a public
constructor that takes a single string as its only parameter.

> [!TIP]
> In fact, primitive types aren't really special to RegExtract&mdash;they're simply examples of types that implement a 
> static `.Parse(string)` method.

## Extracting named capture groups to properties 

# Performance and troubleshooting

## Creating a re-usable `ExtractionPlan`

## Inspecting an extraction plan

# Advanced examples

## Date from email header
```csharp
DateTime date = "Date: Mon, 7 Dec 2020 19:43:24 -0800".Extract<DateTime>(@"Date: (.*)");
```
## List of words
```csharp
List<string> words = "The quick brown fox jumps over the lazy dog.".Extract<List<string>>(@"((\w+)\W*)+");
```
## Alternation
```csharp
var (n,s) = "str".Extract<(int?,string)>(@"(\d+)|(.*)");
```
## Parsing fields
```csharp
var (n1,y1,e1) = "Hello, earthling, from 2077!".Extract<ParseResult>(@"Hello, (.*), from (?:(\d+)|(.*))!");
var (n2,y2,e2) = "Hello, martian, from earth!".Extract<ParseResult>(@"Hello, (.*), from (?:(\d+)|(.*))!");

record ParseResult(string name, int? year, string loc);
```
## Enums and Flags
```csharp
var mode = "OpenOrCreate".Extract<FileMode>(@".*");
var flags = "Public,Static".Extract<BindingFlags>(".*");
```

# Regular Expression reference

# History
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