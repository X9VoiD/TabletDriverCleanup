using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace TabletDriverCleanup.Services;

public class RegexCache
{
    private readonly RegexOptions _defaultOptions;
    private readonly Dictionary<string, Regex> _cache = new();

    public RegexCache(RegexOptions options = RegexOptions.NonBacktracking)
    {
        _defaultOptions = options;
    }

    [return: NotNullIfNotNull("pattern")]
    public Regex? GetRegex(string? pattern)
    {
        return GetRegex(pattern, _defaultOptions);
    }

    [return: NotNullIfNotNull("pattern")]
    public Regex? GetRegex(string? pattern, RegexOptions options)
    {
        if (pattern is null)
            return null;

        ref var regex = ref CollectionsMarshal.GetValueRefOrAddDefault(_cache, pattern, out bool exists);
        if (!exists)
            regex = new Regex(pattern, options);

        return regex!;
    }
}