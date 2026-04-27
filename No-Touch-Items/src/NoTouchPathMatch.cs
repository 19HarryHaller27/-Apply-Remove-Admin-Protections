using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;

namespace NoTouchItems;

public static class NoTouchPathMatch
{
    private static readonly ConcurrentDictionary<string, Regex> RegexByPattern = new();

    public static bool Matches(AssetLocation? code, string patternRaw)
    {
        if (code is null || string.IsNullOrWhiteSpace(patternRaw))
        {
            return false;
        }

        string p = patternRaw.Trim();
        string testPath = code.Path;
        string testFull = code.ToShortString();
        Regex rx = Rg(p);

        if (p.Contains(':', StringComparison.Ordinal))
        {
            return rx.IsMatch(testFull);
        }
        // bare path: match only path (after domain) or full code
        return rx.IsMatch(testPath) || rx.IsMatch(testFull);
    }

    private static Regex Rg(string pattern) =>
        RegexByPattern.GetOrAdd(
            pattern,
            static p =>
        {
            string s = Regex.Escape(p)
                .Replace(
                    "\\*",
                    ".*")
                .Replace(
                    "\\?",
                    ".");
            return new Regex(
                "^" + s + "$",
                RegexOptions.CultureInvariant
                | RegexOptions.IgnoreCase
                | RegexOptions.Compiled);
        });
}
