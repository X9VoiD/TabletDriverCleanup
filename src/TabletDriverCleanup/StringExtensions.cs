using System.Text.RegularExpressions;

namespace TabletDriverCleanup;

public static class StringExtensions
{
    public static bool NullableMatch(this Regex? regex, string? property)
    {
        if (regex is null)
            return true;

        return regex.IsMatch(property ?? "");
    }

    public static bool NullableMatch<T>(this Regex? regex, T property) where T : IEnumerable<string>
    {
        if (regex is null)
            return true;

        return property.Any(regex.IsMatch);
    }

    public static Regex ToRegex(this string str) => new(str);
}