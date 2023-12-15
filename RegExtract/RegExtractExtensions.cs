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

        public static T? Extract<T>(this string str, ExtractionPlan<T> plan)
        {
            return plan.Extract(str);
        }

        public static T? Extract<T>(this string str, RegExtractOptions options = RegExtractOptions.None)
        {
            var rx = GetRegexFromType(typeof(T));   

            return Extract<T>(str, rx, options);
        }

        static Regex GetRegexFromType(Type type)
        {
            var field = type.GetField("REGEXTRACT_REGEX_PATTERN", BindingFlags.Public | BindingFlags.Static);
            if (field is not { IsLiteral: true, IsInitOnly: false })
                throw new ArgumentException("No string, Regex, or Match provided, and extraction type doesn't have public const string REGEXTRACT_REGEX_PATTERN.");
            string rxPattern = (string)field.GetValue(null);

            RegexOptions rxOptions = RegexOptions.None;
            field = type.GetField("REGEXTRACT_REGEX_OPTIONS", BindingFlags.Public | BindingFlags.Static);
            if (field is { IsLiteral: true, IsInitOnly: false }) rxOptions = (RegexOptions)field.GetValue(null);

            return new Regex(rxPattern, rxOptions);
        }
        
        public static IEnumerable<T> Extract<T>(this IEnumerable<string> str, string rx, RegExtractOptions options = RegExtractOptions.None)
        {
            return Extract<T>(str, rx, RegexOptions.None, options);
        }

        public static IEnumerable<T> Extract<T>(this IEnumerable<string> str, string rx, RegexOptions rxOptions, RegExtractOptions options = RegExtractOptions.None)
        {
            return Extract<T>(str, new Regex(rx, rxOptions), options);
        }

        public static IEnumerable<T> Extract<T>(this IEnumerable<string> str, ExtractionPlan<T> plan)
        {
            return str.Select(plan.Extract);
        }

        public static IEnumerable<T> Extract<T>(this IEnumerable<string> str, RegExtractOptions options = RegExtractOptions.None)
        {
            var rx = GetRegexFromType(typeof(T));
            return Extract<T>(str, rx, options);
        }

        public static IEnumerable<T> Extract<T>(this IEnumerable<string> str, Regex rx, RegExtractOptions options = RegExtractOptions.None)
        {
            var plan = ExtractionPlan<T>.CreatePlan(rx, options);
            return str.Select(s => plan.Extract(rx.Match(s)));
        }

        public static bool TryExtract<T>(this string str, string rx, out T result, RegExtractOptions options = RegExtractOptions.None)
        {
            return TryExtract<T>(str, rx, RegexOptions.None, out result, options);
        }

        public static bool TryExtract<T>(this string str, string rx, RegexOptions rxOptions, out T result, RegExtractOptions options = RegExtractOptions.None)
        {
            var match = Regex.Match(str, rx, rxOptions);

            var plan = ExtractionPlan<T>.CreatePlan(new Regex(rx));
            return plan.TryExtract(match, out result);
        }

        public static bool TryExtract<T>(this string str, Regex rx, out T result, RegExtractOptions options = RegExtractOptions.None)
        {
            var match = rx.Match(str);
            var plan = ExtractionPlan<T>.CreatePlan(rx);
            return plan.TryExtract(match, out result);
        }

        public static bool TryExtract<T>(this string str, ExtractionPlan<T> plan, out T result)
        {
            return plan.TryExtract(str, out result);
        }

        public static bool TryExtract<T>(this string str, out T result, RegExtractOptions options = RegExtractOptions.None)
        {
            return TryExtract(str, GetRegexFromType(typeof(T)), out result, options);
        }

        public static bool TryExtract<T>(this IEnumerable<string> str, string rx, out IEnumerable<T> result, RegExtractOptions options = RegExtractOptions.None)
        {
            return TryExtract(str, rx, RegexOptions.None, out result, options);
        }

        public static bool TryExtract<T>(this IEnumerable<string> str, string rx, RegexOptions rxOptions, out IEnumerable<T> result, RegExtractOptions options = RegExtractOptions.None)
        {
            return TryExtract(str, new Regex(rx, rxOptions), out result, options);
        }

        public static bool TryExtract<T>(this IEnumerable<string> str, ExtractionPlan<T> plan, out IEnumerable<T> result)
        {
            var anyFailure = false;
            result = str.Select(s =>
            {
                if (plan.TryExtract(s, out var result))
                {
                    return result;
                }
                anyFailure = true;
                return default!;
            });
            return anyFailure;
        }

        public static bool TryExtract<T>(this IEnumerable<string> str, out IEnumerable<T> result, RegExtractOptions options = RegExtractOptions.None)
        {
            return TryExtract(str, GetRegexFromType(typeof(T)), out result, options);
        }

        public static bool TryExtract<T>(this IEnumerable<string> str, Regex rx, out IEnumerable<T> result, RegExtractOptions options = RegExtractOptions.None)
        {
            var plan = ExtractionPlan<T>.CreatePlan(rx, options);
            var anyFailure = false;
            result = str.Select(s =>
            {
                if (plan.TryExtract(rx.Match(s), out var result))
                {
                    return result;
                }
                anyFailure = true;
                return default!;
            });
            return anyFailure;
        }
    }
}
