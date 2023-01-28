using System.Collections.Immutable;

namespace PnpUtil;

public interface IPnpUtilParseable<T> where T : IPnpUtilParseable<T>
{
    public static ImmutableArray<T> ParseEnumerable(string output)
    {
        var lines = output.Split("\r\n");

        // Skip past the header
        return ParseEnumerable(lines, 2, lines.Length - 1, out _);
    }

    internal static ImmutableArray<T> ParseEnumerable(string[] lines, int startingIndex, int endingIndex, out int linesParsed)
    {
        var builder = ImmutableArray.CreateBuilder<T>();

        var i = startingIndex;
        while (i < endingIndex)
        {
            if (string.IsNullOrEmpty(lines[i]))
            {
                i++;
                continue;
            }

            var device = T.Parse(lines, i, lines.Length - 1, out var linesParsed2);
            i += linesParsed2;

            builder.Add(device);
        }

        linesParsed = i - startingIndex;
        return builder.ToImmutable();
    }

    public static abstract T Parse(string[] lines, int startingIndex, int endingIndex, out int linesParsed);
}