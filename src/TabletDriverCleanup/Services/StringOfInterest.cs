using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace TabletDriverCleanup.Services;

public static class StringOfInterest
{
    [StringSyntax(StringSyntaxAttribute.Regex)]
    private static readonly string[] _stringsOfInterest = new[]
    {
        "10moon",
        "Acepen",
        "Artisul",
        "Digitizer",
        "EMR",
        "filtr",
        "Gaomon",
        "Genius",
        "Huion",
        "Kenting",
        "libwdi",
        "Lifetec",
        "Monoprice",
        "Parblo",
        "RobotPen",
        "Tablet",
        "UC[-| ]?Logic",
        "UGEE",
        "Veikk",
        "ViewSonic",
        @"v\w*hid",
        "Wacom",
        "WinUSB",
        "XenceLabs",
        "XENX",
        "XP[-| ]?Pen",
    };

    private static readonly string[] _counterInterest = new[]
    {
        "android"
    };

    private static readonly RegexCache _regexCache = new(RegexOptions.NonBacktracking | RegexOptions.IgnoreCase);

    public static bool IsCandidate(string? str)
    {
        if (str is null)
            return false;

        foreach (var soi in _stringsOfInterest)
        {
            var regex = _regexCache.GetRegex(soi);

            if (regex.IsMatch(str) &&!IsCounterCandidate(str))
                return true;
        }

        return false;
    }

    public static bool IsCandidate<T>(T strs) where T : IEnumerable<string?>
    {
        foreach (var str in strs)
        {
            if (IsCandidate(str))
                return true;
        }

        return false;
    }

    public static bool IsCandidate(params string?[] strs)
    {
        return IsCandidate<string?[]>(strs);
    }

    private static bool IsCounterCandidate(string? str)
    {
        if (str is null)
            return false;

        foreach (var soi in _counterInterest)
        {
            var regex = _regexCache.GetRegex(soi);

            if (regex.IsMatch(str))
                return true;
        }

        return false;
    }
}