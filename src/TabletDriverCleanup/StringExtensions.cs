using System.Diagnostics;
using System.Text.RegularExpressions;

namespace TabletDriverCleanup;

public static partial class StringExtensions
{
    public static bool NullableMatch(this Regex? regex, string? property)
    {
        if (regex is null)
            return true;

        return regex.IsMatch(property ?? string.Empty);
    }

    public static bool NullableMatch<T>(this Regex? regex, T property) where T : IEnumerable<string>
    {
        if (regex is null)
            return true;

        return property.Any(regex.IsMatch);
    }

    public static void ExtractToArgs(this string str, out string command, out string? args)
    {
        var strSpan = str.AsSpan();
        if (strSpan[0] == '"' || strSpan[0] == '\'')
        {
            var quoteChar = str[0];
            bool isEscaped = false;
            bool quoteEnded = false;
            for (var i = 1; i < strSpan.Length; i++)
            {
                var curChar = strSpan[i];
                if (curChar == ' ' && !isEscaped && quoteEnded)
                {
                    command = strSpan[0..i].ToString();
                    args = strSpan[(i + 1)..].ToString().Trim();
                    return;
                }
                else if (curChar == quoteChar && !isEscaped)
                {
                    quoteEnded = !quoteEnded;
                }
                else if (curChar == '\\')
                {
                    isEscaped = !isEscaped;
                }
                else
                {
                    isEscaped = false;
                }
            }

            if (!quoteEnded)
                throw new ArgumentException("Invalid string format");

            command = str;
            args = null;
            return;
        }
        else
        {
            var spaceIndex = strSpan.IndexOf(' ');
            if (spaceIndex != -1)
            {
                command = strSpan[..spaceIndex].ToString();
                args = strSpan[(spaceIndex + 1)..].ToString().Trim();
                return;
            }
            else
            {
                command = str;
                args = null;
                return;
            }
        }
    }

    public static string RemoveQuotes(this string str)
    {
        if (str[0] == '"' || str[0] == '\'')
        {
            return str[1..^1];
        }

        return str;
    }

    public static ProcessStartInfo ToProcessStartInfo(this string str, bool workaroundMissingQuote = false)
    {
        if (!workaroundMissingQuote)
        {
            str.ExtractToArgs(out string command, out string? args);
            return new ProcessStartInfo(command, args!);
        }

        var regex = ExeRegex();
        var match = regex.Match(str);
        if (match.Success)
        {
            var exe = match.Groups["command"].Value;
            var args = match.Groups["args"].Value;
            return new ProcessStartInfo(exe, args);
        }
        else
        {
            return new ProcessStartInfo(str);
        }
    }

    public static Regex ToRegex(this string str) => new(str);

    [GeneratedRegex(@"(?<command>.+?\.exe) (?<args>.*)")]
    private static partial Regex ExeRegex();
}