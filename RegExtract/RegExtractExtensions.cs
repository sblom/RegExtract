using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using static RegExtract.ExtractionPlanner;

namespace RegExtract
{
    public enum RegExtractOptions
    {
        None = 0x0,
        Strict = 1<<0,
        Nested = 1<<1
    }

    public static class RegExtractExtensions
    {
        public static T? Extract<T>(this Match match, RegExtractOptions options = RegExtractOptions.None, string?[]? groupNames = null)
        {
            if (!match.Success)
                throw new ArgumentException("Regex failed to match input.");

            if (groupNames == null)
            {
                groupNames = new string?[match.Groups.Count];
#if !NETSTANDARD2_0 && !NET40
                groupNames = match.Groups.Select(g => g.Name).ToArray();
#endif
            }

            if (options.HasFlag(RegExtractOptions.Nested))
            {
                var plan = CreateExtractionPlan(match.Groups.AsEnumerable(), groupNames, typeof(T));
                return (T)plan.Execute();
            }
            else
                return ExtractionPlanner.Extract<T>(match.Groups.AsEnumerable().Zip(groupNames ?? Enumerable.Repeat<string?>(null, int.MaxValue), (group,name) => (group,name)), options);
        }

        public static T? Extract<T>(this string str, string rx, RegExtractOptions options = RegExtractOptions.None)
        {
            return Extract<T>(str, rx, RegexOptions.None, options);
        }

        public static T? Extract<T>(this string str, string rx, RegexOptions rxOptions, RegExtractOptions options = RegExtractOptions.None)
        {
            var match = Regex.Match(str, rx, rxOptions);

            string[]? groupNames = null;
#if NETSTANDARD2_0 || NET40
             groupNames = new Regex(rx).GetGroupNames();
#endif

            return Extract<T>(match, options, groupNames);
        }

        public static T? Extract<T>(this string str, Regex rx, RegExtractOptions options = RegExtractOptions.None)
        {
            var match = rx.Match(str);

            string[]? groupNames = null;
#if NETSTANDARD2_0 || NET40
            groupNames = rx.GetGroupNames();
#endif
            
            return Extract<T>(match, options,groupNames);
        }

        public static T? Extract<T>(this string str, RegExtractOptions options = RegExtractOptions.None)
        {
            var field = typeof(T).GetField("REGEXTRACT_REGEX_PATTERN", BindingFlags.Public | BindingFlags.Static);
            if (field is not { IsLiteral: true, IsInitOnly: false })
                throw new ArgumentException("No string, Regex, or Match provided, and extraction type doesn't have public const string REGEXTRACT_REGEX_PATTERN.");
            string rxPattern = (string)field.GetValue(null);

            RegexOptions rxOptions = RegexOptions.None;
            field = typeof(T).GetField("REGEXTRACT_REGEX_OPTIONS", BindingFlags.Public | BindingFlags.Static);
            if (field is { IsLiteral: true, IsInitOnly: false }) rxOptions = (RegexOptions)field.GetValue(null);

            var match = Regex.Match(str, rxPattern, rxOptions);

            string[]? groupNames = null;
#if NETSTANDARD2_0 || NET40
            groupNames = new Regex(rxPattern, rxOptions).GetGroupNames();
#endif

            return Extract<T>(match, options, groupNames);
        }
    }
}
