using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace TabletDriverCleanup;

public class HeuristicSample
{
    public string Text { get; }

    public Regex TextRegex { get; }

    public HeuristicSample([StringSyntax(StringSyntaxAttribute.Regex)] string text)
    {
        Text = text;
        TextRegex = Text.ToRegex();
    }
}