using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace TabletDriverCleanup;

public class DeviceToUninstall
{
    public string FriendlyName { get; }
    public string DeviceDescription { get; }
    public string? ClassName { get; }
    public string? ManufacturerName { get; }

    public string? ReplacementDriver { get; }
    public bool RemoveDevice => ReplacementDriver == null;

    public Regex DeviceDescriptionRegex { get; }
    public Regex? ClassNameRegex { get; }
    public Regex? ManufacturerNameRegex { get; }

    public DeviceToUninstall(
        string friendlyName,
        [StringSyntax(StringSyntaxAttribute.Regex)] string deviceDescription,
        [StringSyntax(StringSyntaxAttribute.Regex)] string? className = null,
        [StringSyntax(StringSyntaxAttribute.Regex)] string? manufacturerName = null,
        string? replacementDriver = null)
    {
        FriendlyName = friendlyName;
        DeviceDescription = deviceDescription;
        ClassName = className;
        ManufacturerName = manufacturerName;

        ReplacementDriver = replacementDriver;

        DeviceDescriptionRegex = DeviceDescription.ToRegex();
        ClassNameRegex = ClassName?.ToRegex();
        ManufacturerNameRegex = ManufacturerName?.ToRegex();
    }
}