using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace RegExtract
{
    public enum RegExtractOptions
    {
        None = 0x0,
        Strict = 1<<0
    }

    public static class RegExtractExtensions
    {
        public static T? Extract<T>(this string str, string rx, RegExtractOptions options = RegExtractOptions.None)
        {
            return Extract<T>(str, rx, RegexOptions.None, options);
        }

        public static T? Extract<T>(this string str, string rx, RegexOptions rxOptions, RegExtractOptions options = RegExtractOptions.None)
        {
            var match = Regex.Match(str, rx, rxOptions);

            var plan = ExtractionPlan<T>.CreatePlan(new Regex(rx));
            return (T)plan.Extract(match);
        }

        public static T? Extract<T>(this string str, Regex rx, RegExtractOptions options = RegExtractOptions.None)
        {
            var match = rx.Match(str);

            var plan = ExtractionPlan<T>.CreatePlan(rx);
            return (T)plan.Extract(match);
        }

        public static T? Extract<T>(this string str, Regex rx, Match match, RegExtractOptions options = RegExtractOptions.None)
        {
            var plan = ExtractionPlan<T>.CreatePlan(rx);
            return (T)plan.Extract(match);
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

            return Extract<T>(str, new Regex(rxPattern), match, options);
        }
    }
}
