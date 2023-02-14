using System.Diagnostics.CodeAnalysis;
using TabletDriverCleanup.Services;

namespace TabletDriverCleanup.Modules;

public class DriverToUninstall : IObjectToUninstall
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

    public bool Matches(RegexCache regexCache, object obj)
    {
        if (obj is not Driver driver)
            return false;

        var originalNameRegex = regexCache.GetRegex(OriginalName);
        var providerNameRegex = regexCache.GetRegex(ProviderName);

        return originalNameRegex.NullableMatch(driver.InfOriginalName) &&
            providerNameRegex.NullableMatch(driver.Provider) &&
            (ClassGuid is not Guid guid || guid == driver.ClassGuid);
    }

    public override string ToString()
    {
        return FriendlyName;
    }
}