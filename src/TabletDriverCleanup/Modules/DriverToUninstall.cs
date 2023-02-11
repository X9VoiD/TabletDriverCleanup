using System.Diagnostics.CodeAnalysis;

namespace TabletDriverCleanup.Modules;

public class DriverToUninstall
{
    public string FriendlyName { get; init; }
    public string OriginalName { get; }
    public string? ProviderName { get; }
    public Guid? ClassGuid { get; }

    public DriverToUninstall(
        string friendlyName,
        [StringSyntax(StringSyntaxAttribute.Regex)] string originalName,
        [StringSyntax(StringSyntaxAttribute.Regex)] string? providerName = null,
        Guid? classGuid = null)
    {
        FriendlyName = friendlyName;
        OriginalName = originalName;
        ProviderName = providerName;
        ClassGuid = classGuid;
    }
}