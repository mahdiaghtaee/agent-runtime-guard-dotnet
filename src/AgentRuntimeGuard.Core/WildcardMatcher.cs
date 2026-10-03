using System.Text.RegularExpressions;

namespace AgentRuntimeGuard.Core;

internal static class WildcardMatcher
{
    public static bool IsMatch(string pattern, string value)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(value);

        if (pattern == "*")
        {
            return true;
        }

        var expression =
            "^" + Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal) + "$";

        return Regex.IsMatch(
            value,
            expression,
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
            TimeSpan.FromMilliseconds(50));
    }
}
