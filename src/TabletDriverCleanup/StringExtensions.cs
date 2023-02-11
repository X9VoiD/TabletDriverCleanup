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

    public static Regex ToRegex(this string str) => new(str);
}