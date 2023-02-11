using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace TabletDriverCleanup.Services;

public class RegexCache
{
    private readonly Dictionary<string, Regex> _cache = new();

    [return: NotNullIfNotNull("pattern")]
    public Regex? GetRegex(string? pattern)
    {
        if (pattern is null)
            return null;

        ref var regex = ref CollectionsMarshal.GetValueRefOrAddDefault(_cache, pattern, out bool exists);
        if (!exists)
            regex = new Regex(pattern, RegexOptions.NonBacktracking);

        return regex!;
    }
}