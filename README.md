# RegExtract
Quick and dirty idiomatic C# line parser that extracts text into practical data types.

[![dotnet](https://github.com/sblom/RegExtract/workflows/dotnet/badge.svg)](https://github.com/sblom/RegExtract/actions)
[![NuGet](https://img.shields.io/nuget/v/RegExtract.svg)](https://www.nuget.org/packages/RegExtract/)
[![Downloads](https://img.shields.io/nuget/dt/RegExtract.svg)](https://www.nuget.org/packages/RegExtract/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Table of Contents
- [RegExtract](#regextract)
  - [Table of Contents](#table-of-contents)
  - [Release History (newest first)](#release-history-newest-first)
- [Using RegExtract](#using-regextract)
  - [Basic extraction to `ValueTuple`](#basic-extraction-to-valuetuple)
  - [Extracting from multiple input strings (`IEnumerable<string>`)](#extracting-from-multiple-input-strings-ienumerablestring)
  - [Extracting to collections (such as `List<T>`)](#extracting-to-collections-such-as-listt)
  - [Nullable types](#nullable-types)
  - [Nesting compound types (such as tuples) and collections](#nesting-compound-types-such-as-tuples-and-collections)
    - [Tuple that contains Collections](#tuple-that-contains-collections)
    - [Collection that contains a tuple](#collection-that-contains-a-tuple)
    - [Collection that contains a collection](#collection-that-contains-a-collection)
  - [Collections with more than one argument (including `Dictionary<,>`)](#collections-with-more-than-one-argument-including-dictionary)
  - [`record`s and other compound types](#records-and-other-compound-types)
  - [Extracting named capture groups to properties](#extracting-named-capture-groups-to-properties)
  - [Other supported types](#other-supported-types)
  - [Including REGEXTRACT\_REGEX\_PATTERN templates on types](#including-regextract_regex_pattern-templates-on-types)
- [Performance and troubleshooting](#performance-and-troubleshooting)
  - [Creating a re-usable `ExtractionPlan`](#creating-a-re-usable-extractionplan)
  - [Inspecting an extraction plan](#inspecting-an-extraction-plan)
- [Regular Expression reference](#regular-expression-reference)
- [History](#history)

## Release History (newest first)

|Release Number |Release Date | Main Features |
|--|--|--|
| 3.0 | FUTURE ROADMAP | Source Generator support to eliminate run-time reflection
| 2.1 | December 15, 2023 | Added caching for up to 6x speedup<br>Made tuples less magic |
| 2.0 | December 14, 2023 | Rewrote planning engine with better Collections support |
| 1.0 | December 20, 2020 | First modern release with tree-based extraction planner |
| 0.9 | early December 2020 | Pre-release prototypes |
<details>
<summary>History of pre-release versions</summary>

|Release Number |Release Date | Main Features |
|--|--|--|
| 0.9.24 | December 2020 | Extraction planner fully operational |
| 0.9.19 | December 2020 | Prototype extraction planner to support nested types |
| 0.9.16 | December 2020 | Add support for REGEXTRACT_REGEX_PATTERN templates |
| 0.9.11 | December 2020 | Add support for Enums |
| 0.9.10 | December 2020 | More support for Lists |
| 0.9.6 | December 2020 | Add support for Lists and Nullables |
| 0.9.4 | December 2020 | Add support for named capture groups initializing properties |
| 0.9.2 | December 2020 | Add positional records |
| 0.9 | December 2020 | Extract capture groups to tuples, and that's all |

</details>

# Using RegExtract

## Basic extraction to `ValueTuple`

Let's say you have a string `2-10 c: abcdefghi`, consisting of a two `int`s separated by a dash (-), a `char` followed by a colon (:), and a `string`.

You could use the regular expression `@"(\d+)-(\d+) (.): (.*)"` to extract that into a tuple `(int min, int max, char ch, string str)`.
Or you could use `@"((\d+)-(\d+)) (.): (.*)"` to extract into a nested tuple `((int min, int max) range, char ch, string str)`. 

> [!TIP]
> If you need a primer on helpful regular expression syntax, see the [Regular Expression Examples](#regular-expression-examples) section below.

In C# code, those two examples would look like:

```cs
using RegExtract;

var input = "2-10 c: abcdefghi";

var flat_tuple   = input.Extract<( int min, int max,        char ch, string str)>(@"(\d+)-(\d+) (.): (.*)");
var nested_tuple = input.Extract<((int min, int max) range, char ch, string str)>(@"((\d+)-(\d+)) (.): (.*)");
```

> [!NOTE]
> The nesting of your capture groups (parts wrapped in `()`) in your regular expression must match the nesting of your type hierarchy.

## Extracting from multiple input strings (`IEnumerable<string>`)

There are many variations on RegExtract extension methods, but there are two that you will use most often.
The first one is the `.Extract<T>()` method demonstrated above.
It's an extension method on `string`, and returns a single fully constructed instance of your type hierarchy `T`.
The other one is very similar, but it accepts any `IEnumerable<string>`.

Here's an example of using the `IEnumerable<string>` extension method:

```cs
using RegExtract;

var inputs = new[] {
  "2-10 c: abcdefghi",
  "3-7 e: qwertyuiop"
};

IEnumerable<(int,int,char,string)> results = 
    inputs.Extract<( int min, int max, char ch, string str)>(@"(\d+)-(\d+) (.): (.*)");
```

Notice that the actual `.Extract<>()` call looks nearly identical to the version that takes a single `string`.
This makes it trivial to switch between extracting a single instance and extracting from each string in an `IEnumerable<string>`.

> [!TIP]
> These are the two most common RegExtract methods to use, but if you're going to be using the same extraction plan multiple times, you should first [create a reusable `ExtractionPlan`](#creating-a-re-usable-extractionplan), so that RegExtract only has to parse your regular expression and type hierarchy once.

## Extracting to collections (such as `List<T>`)

In addition to arbitrarily long `ValueTuple`s as demonstrated above, RegExtract supports any collection type that works with [C#'s Collection Initializer syntax](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/object-and-collection-initializers#collection-initializers) (commonly `List<>`, `HashSet<>`, and `Dictionary<,>`).

> [!TIP]
> C# collection initializers work with *any collection type that implements `IEnumerable` [the non-generic one, in particular] and has Add with the appropriate signature*.

To extract a list, you should include a capture group in your regular expression that repeats.
For example, to break a sentence up into individual words, you can do something like:

```cs
using RegExtract;

var input = "The quick brown fox jumps over the lazy dog.";

var words_with_trailing_spaces = input.Extract<List<string>>(@"(\w+ ?)+");
var words_without_spaces       = input.Extract<List<string>>(@"((\w+) ?)+");
```

Notice in the first example (`words_with_trailing_spaces`), there is only one capture group, and everything inside it is treated as part of the match.
As a result, the strings in the List include trailing spaces (except for "dog", which stopped matching before the trailing period (.)).

In the second example (`words_without_spaces`), an optional final set of parens was included immediately around `\w+`.
As a result, the strings in the second list will only include the words themselves without trailing spaces.

> [!NOTE]
> As illustrated by the `words_without_spaces` example, you can always optionally include an extra capture group to capture only a relevant subpart inside the repeating capture group of any **Collection** type.
>
> This is useful if the repeated capture group includes optional separators such as spaces, commas, semicolons, etc., and it allows you to include only the interesting part without the separator.

## Nullable types

Any time a type hierarchy expects a value but there's no corresponding Capture (because of an optional capture group, for example), RegExtract considers the extracted value to be `null`.
For reference types, this works exactly how you'd expect.
For value types, you'll get an `InvalidCastException ("Null object cannot be converted to a value type.")` unless you have marked the value type as nullable in your type hierarchy.

> [!NOTE]
> Collection types will always be constructed and will never be extracted as a `null`.
> Unlike missing Captures for non-collection values, that there are no matches, it will simply be empty.

An example of extracting to a nullable type (or not):

```csharp
using RegExtract;

// This will succeed because int? can be null.
var nullable     = "".Extract<int?>(@"(\d+)");
//                               ^Nullable

// This will throw an exception because \d+ doesn't match anything
// and the int value is required by the type system.
var not_nullable = "".Extract<int >(@"(\d+)");
//                               ^Not nullable
```

> [!TIP]
> You can use nullable types in combination with the regular expression alternation operator (`|`) to extract to a different type depending on the details of the match.

An example of using nullable types to support regular expression alternation (`|`):
```csharp
var (n,s) = "str".Extract<(int?,string)>(@"(\d+)|(.*)");
```

## Nesting compound types (such as tuples) and collections

You'll frequently find that you need to nest a **compound type** (such as a tuple) inside a **collection** or that you need to nest a **collection** inside a **compound type**.
RegExtract can handle arbitrarily deeply nested mixes of any supported data types.

### Tuple that contains Collections
```csharp
using RegExtract;

var input = "Item #1: 27 61 49 58 44 2 69 78";

var result = input.Extract<(int itemno, HashSet<int> set)>(@"Item #(\d+): (\d+ ?)+");
```

> [!TIP]
> As you can see in this examples, a `HashSet<>` works just like a `List<>`. 

### Collection that contains a tuple
```csharp
using RegExtract;

var input = "red 10, blue 25, green 12, yellow 19";

var result = input.Extract<List<(string color, int count)>>(@"((\w+) (\d+),? ?)+");
```

### Collection that contains a collection
```csharp
using RegExtract;

var input = "The quick brown fox jumps over the lazy dog";

var result = input.Extract<List<List<char>>>(@"((\w)+ ?)+");
```

## Collections with more than one argument (including `Dictionary<,>`)

C# collection initializers will work with `.Add()` methods that take more than one parameter, such as the `.Add(TKey key, TValue value)` that `Dictionary<,>` implements.
RegExtract doesn't have the benefit of inferring generic type arguments from examples of parameters, however, since everything is a `string` before extraction.
So, instead, RegExtract will only consider an `.Add()` method whose parameter types match the generic arguments `TKey` and `TValue`.

Example using a `Dictionary<string,int>`:
```csharp
using RegExtract;

var input = "red 10, blue 25, green 12, yellow 19";

var result = input.Extract<Dictionary<string, int>>(@"((\w+) (\d+),? ?)+");
```

> [!TIP]
> RegExtract doesn't yet support having, for example, a capture group with the `value` before the `key`.
> (They have to be in the order that the collection's `.Add()` method expects them.)
> 
> You can work around this by capturing to a `List<(TValue value, TKey key)>` and then using `list.ToDictionary(vk => vk.key, vk => vk.Value)` to convert to a `Dictionary<,>` that's organized the way you want.


## `record`s and other compound types

You can build almost anything you need using `ValueTuple`s and `List<>`s, and for simple, ad hoc scenarios that's often where I begin and end.

However, when it comes time to extract inputs to more richly modeled types, you'll use RegExtract's support for types such as `record`s that have a single obvious constructor (some might say a [primary constructor!](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/instance-constructors#primary-constructors)) with the number of parameters corresponding to the number of capture groups in your regular expression.
(Strictly speaking, it doesn't have to be a record, and you don't have to use primary constructor syntax&mdash;absolutely any type with a constructor of the right shape is fine.)

> [!INFO]
> For custom compound types such as `record`s or `struct`s or `class`es, RegExtract looks for a single public constructor that takes the same number of arguments as the number of capture groups nested inside the compound type's capture group.
>
> It then uses the types of the constructor arguments to determine what types to construct for the nested capture groups.

Here's an example using a couple of nested `record` types and `List`s:
```csharp
using RegExtract;

var input = "Game 14: 9 green, 4 red; 6 blue, 1 red, 7 green; 3 blue, 5 green";

var game = input.Extract<Game>(@"Game (\d+): (((\d+) (\w+),? ?)+;? ?)+");

record Game(int id, List<Draw> draws);
record Draw(List<(int count, string color)> colors);
```

## Extracting named capture groups to properties

All of the examples of compound types so far make use of constructors with positional semantics.
RegExtract uses typical (non-named) capture groups as parameters destined for a tuple slot or a constructor parameter.

Regular expressions also support named capture groups.
They look like `(?<name>pattern_goes_here)`.
When RegExtract encounters a named capture group, the captures from it are used to call a property setter on the type being extracted after the type is fully constructed from (non-named) positional capture groups.

A simple example:
```csharp
using RegExtract;

var input = 

var result = input.Extract<template>(@"(?<lo>\d+)-(?<hi>\d+) (?<ch>.): (?<pwd>.*)");

class template {
    public int lo { get; set; }
    public int hi { get; set; }
    public char ch { get; set; }
    public string pwd { get; set; }
}
```

A more complex example that mixes positional (constructor) parameters with properties set by named captures:
```csharp
var input = "faded yellow bags contain 4 mirrored fuchsia bags, 4 dotted indigo bags, 3 faded orange bags, 5 plaid crimson bags.";

var result = input.Extract<BagDescription>(@"^(.+) bags contain(?<contents> (?<num>\d+) (?<type>.*?) bags?[,.])+$");

result.Dump();

// Notice that name is a constructor parameter, and
// is set by a capture group without a name, specifically `(.+)`.
record BagDescription(string name)
{
    // On the other hand, contents is set by the named capture group `(?<contents>...)`.
    public List<IncludedBags> contents {get;set;}
};

// More properties set by named capture groups.
record IncludedBags
{
    public int? num { get; set; }
    public string type { get; set; }
}
```

## Other supported types

In addition to tuples, custom compound types, and collections as discussed above, *all of .NET's primitive types* are supported, as well as any `Enum`, any type that implements a static `.Parse(string)` method, and, as a last resort, any type with a public constructor that takes a single string as its only parameter.

> [!TIP]
> In fact, .NET's primitive types aren't really special to RegExtract&mdash;they're simply examples of types that implement a static `.Parse(string)` method.

A couple of Enum examples:
```csharp
var mode = "OpenOrCreate".Extract<FileMode>(@".*");
var flags = "Public,Static".Extract<BindingFlags>(".*");
```

## Including REGEXTRACT_REGEX_PATTERN templates on types
You can set a default RegExtract regular expression pattern on a compound type to enable RegExtract to know how to extract the type without passing the regular expression pattern as an argument to `.Extract<>()`.

You do this by placing a `public const string REGEXTRACT_REGEX_PATTERN` on the type.
You can also set default regular expression options by setting `public const string REGEXTRACT_REGEX_OPTIONS`.

For example:
```csharp
using RegExtract;

var result = "add -12".Extract<WithTemplate>();

record WithTemplate(string op, int arg)
{
    public const string REGEXTRACT_REGEX_PATTERN = @"(\S+) ([+-]?\d+)";
    public const RegexOptions REGEXTRACT_REGEX_OPTIONS = RegexOptions.None;
}
```

# Performance and troubleshooting

## Creating a re-usable `ExtractionPlan`
RegExtract spends some upfront time parsing regular expressions and inspecting your type hierarchy using reflection.
Reflection in particular is notorious for being slow.

If you're going to be doing the same extraction repeatedly, you can have RegExtract do all of this work upfront.

Your approach should be to first create an `ExtractionPlan` that knows the regular expression and type hierarchy that it will be using to do its extraction.
You can then use this `ExtractionPlan` instead of a regex pattern or Regex.

Example:
```csharp
var plan = ExtractionPlan<((int, int), char, string)>.CreatePlan(new Regex(@"((\d+)-(\d+)) (.): (.*)"));
"2-12 c: abcdefg".Extract(plan);
```

> [!INFO]
> Note that `CreatePlan` takes a `Regex` instead of the string that we've been using throughout the examples in this documentation.
>
> Every call to `.Extract()` that you have seen happens to have a `Regex` overload.
> This is important when you have Source Generated regular expressions, but can also be used in other places where you've already created a `Regex` instance that you may want to use for extraction.

## Inspecting an extraction plan
If you have an `ExtractionPlan`, you can have it pretty-print its understanding of the types and the regular expression that you have asked it to use for its extraction.

```csharp
var plan = ExtractionPlan<((int, int), char, string)>.CreatePlan(new Regex(@"((\d+)-(\d+)) (.): (.*)"));
Console.WriteLine(plan.ToString("x"));
```

This prints the following extraction plan (you can see what capture groups it found):
```
ConstructTuple<((int,int),char,string)>[0] (
  ConstructTuple<(int,int)>[1] (
    StaticParseMethod<int>[2] (),
    StaticParseMethod<int>[3] ()
  ),
  StaticParseMethod<char>[4] (),
  StringCast<string>[5] ()
)

↓--------------↓ ↓--↓: ↓---↓₀
(↓----↓-↓----↓)₁ (.)₄  (.*)₅ 
  (\d+)₂ (\d+)₃
```

# Regular Expression reference

| Regex block | Effect |
| --- | --- |
| ([+-]?\d+) | a positive or negative integer with an optional sign character |
| (\w+) | a string of word characters |
| (.) | a single character |
| ()+ | a list of whatever you put inside the parens |
| (?&lt;name>pattern_goes_here) | a named capture group named 'name' |
| (?: ) | a non-capturing group |

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