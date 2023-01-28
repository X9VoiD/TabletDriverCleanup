using System.Text.RegularExpressions;

namespace PnpUtil;

internal static partial class StringExtensions
{
    public static int DelimitScope(this string[] str, int startIndex)
    {
        var expectedIndent = str[startIndex].DetectIndentLevel();
        if (expectedIndent == 0)
            return 0;

        var i = startIndex + 1;

        for (; i < str.Length; i++)
        {
            var line = str[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var currentIndent = line.DetectIndentLevel();
            if (currentIndent < expectedIndent)
                break;
        }

        return i;
    }

    public static int DetectIndentLevel(this string str)
    {
        var indent = 0;
        foreach (var c in str)
        {
            if (c == ' ')
                indent++;
            else
                break;
        }
        return indent;
    }

    [GeneratedRegex(@"^ *(?<prop>[\w| ]+?): *(?<value>.+?)?$")]
    public static partial Regex GetPropertyRegex();
}