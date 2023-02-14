using System.Runtime.Versioning;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace TabletDriverCleanup.Services;

public class DriverPackage
{
    public bool X86 { get; }

    [JsonIgnore]
    public string KeyName { get; }

    public string? DisplayName { get; }
    public string? DisplayVersion { get; }
    public string? Publisher { get; }
    public string? InstallLocation { get; }
    public string? UninstallString { get; }

    public DriverPackage(
        bool x86,
        string keyName,
        string? displayName,
        string? displayVersion,
        string? publisher,
        string? installLocation,
        string? uninstallString)
    {
        X86 = x86;
        KeyName = keyName;
        DisplayName = displayName;
        DisplayVersion = displayVersion;
        Publisher = publisher;
        InstallLocation = installLocation;
        UninstallString = uninstallString;
    }

    [SupportedOSPlatform("windows")]
    public static DriverPackage FromRegistryKey(RegistryKey key)
    {
        var x86 = key.Name.Contains("Wow6432Node");
        var displayName = key.GetValue("DisplayName") as string;
        var displayVersion = key.GetValue("DisplayVersion") as string;
        var publisher = key.GetValue("Publisher") as string;
        var installLocation = key.GetValue("InstallLocation") as string;
        var uninstallString = key.GetValue("UninstallString") as string;

        return new DriverPackage(x86, key.Name, displayName, displayVersion, publisher, installLocation, uninstallString);
    }
}