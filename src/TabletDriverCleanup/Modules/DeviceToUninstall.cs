using System.Diagnostics.CodeAnalysis;
using TabletDriverCleanup.Services;

namespace TabletDriverCleanup.Modules;

public class DeviceToUninstall : IObjectToUninstall
{
    public string FriendlyName { get; }
    public string DeviceDescription { get; }
    public string? ManufacturerName { get; }
    public string? HardwareId { get; }
    public Guid? ClassGuid { get; }
    public string? ReplacementDriver { get; }
    public bool RemoveDevice => ReplacementDriver == null;

    public DeviceToUninstall(
        string friendlyName,
        [StringSyntax(StringSyntaxAttribute.Regex)] string deviceDescription,
        [StringSyntax(StringSyntaxAttribute.Regex)] string? manufacturerName = null,
        [StringSyntax(StringSyntaxAttribute.Regex)] string? hardwareId = null,
        Guid? classGuid = null)
    {
        FriendlyName = friendlyName;
        DeviceDescription = deviceDescription;
        ManufacturerName = manufacturerName;
        HardwareId = hardwareId;
        ClassGuid = classGuid;
    }

    public bool Matches(RegexCache regexCache, object obj)
    {
        if (obj is not Device device)
            return false;

        var deviceDescriptionRegex = regexCache.GetRegex(DeviceDescription);
        var manufacturerNameRegex = regexCache.GetRegex(ManufacturerName);
        var hardwareIdRegex = regexCache.GetRegex(HardwareId);

        return deviceDescriptionRegex.NullableMatch(device.FriendlyName) &&
            manufacturerNameRegex.NullableMatch(device.Manufacturer) &&
            hardwareIdRegex.NullableMatch(device.HardwareIds) &&
            (ClassGuid is not Guid guid || guid == device.ClassGuid);
    }

    public override string ToString()
    {
        return FriendlyName;
    }
}