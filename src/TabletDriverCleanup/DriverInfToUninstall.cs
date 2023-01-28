using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace TabletDriverCleanup;

public class DriverInfToUninstall
{
    public string FriendlyName { get; init; }
    public string OriginalName { get; }
    public string? ProviderName { get; }
    public string? ClassName { get; }

    public Regex OriginalNameRegex { get; }
    public Regex? ProviderNameRegex { get; }
    public Regex? ClassNameRegex { get; }

    public DriverInfToUninstall(
        string friendlyName,
        [StringSyntax(StringSyntaxAttribute.Regex)] string originalName,
        [StringSyntax(StringSyntaxAttribute.Regex)] string? providerName = null,
        [StringSyntax(StringSyntaxAttribute.Regex)] string? className = null)
    {
        FriendlyName = friendlyName;
        OriginalName = originalName;
        ProviderName = providerName;
        ClassName = className;

        OriginalNameRegex = OriginalName.ToRegex();
        ProviderNameRegex = ProviderName?.ToRegex();
        ClassNameRegex = ClassName?.ToRegex();
    }
}