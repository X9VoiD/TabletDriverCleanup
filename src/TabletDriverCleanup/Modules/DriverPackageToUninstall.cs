using System.Diagnostics.CodeAnalysis;
using TabletDriverCleanup.Services;

namespace TabletDriverCleanup.Modules;

public class DriverPackageToUninstall : IObjectToUninstall
{
    public string FriendlyName { get; }
    public string? DisplayName { get; }
    public string? DisplayVersion { get; }
    public string? Publisher { get; }
    public string UninstallMethod { get; }

    /// <summary>
    /// The uninstaller from UninstallString is a proxy program that launches
    /// the actual uninstaller extracted to a temporary directory. The actual
    /// uninstaller closes the proxy program during uninstall.
    /// </summary>
    public const string Normal = "normal";

    /// <summary>
    /// The uninstaller from UninstallString is a proxy program that launches
    /// the actual uninstaller extracted to a temporary directory and then immediately
    /// exits.
    /// </summary>
    public const string Deferred = "deferred";

    /// <summary>
    /// Use this only if the uninstallation is already handled by other cleanup modules.
    /// </summary>
    public const string RegistryOnly = "registry_only";

    public DriverPackageToUninstall(
        string friendlyName,
        [StringSyntax(StringSyntaxAttribute.Regex)] string? displayName,
        [StringSyntax(StringSyntaxAttribute.Regex)] string? displayVersion,
        [StringSyntax(StringSyntaxAttribute.Regex)] string? publisher,
        string uninstallMethod)
    {
        FriendlyName = friendlyName;
        DisplayName = displayName;
        DisplayVersion = displayVersion;
        Publisher = publisher;
        UninstallMethod = uninstallMethod;
    }

    public bool Matches(RegexCache regexCache, object obj)
    {
        if (obj is not DriverPackage driverPackage)
            return false;

        var displayNameRegex = regexCache.GetRegex(DisplayName);
        var displayVersionRegex = regexCache.GetRegex(DisplayVersion);
        var publisherRegex = regexCache.GetRegex(Publisher);

        return displayNameRegex.NullableMatch(driverPackage.DisplayName)
            && displayVersionRegex.NullableMatch(driverPackage.DisplayVersion)
            && publisherRegex.NullableMatch(driverPackage.Publisher);
    }

    public override string ToString()
    {
        return FriendlyName;
    }
}