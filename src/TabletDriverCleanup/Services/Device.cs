using System.Collections.Immutable;
using System.Diagnostics;

namespace TabletDriverCleanup.Services;

[DebuggerDisplay($"{{{nameof(FriendlyName)}}} ({{{nameof(Class)}}})")]
public class Device
{
    private readonly string? _friendlyName;

    public bool IsGeneric { get; }
    public string InstanceId { get; }
    public ImmutableArray<string> HardwareIds { get; }
    public string? Description { get; }
    public string? FriendlyName => _friendlyName ?? Description;
    public string? Manufacturer { get; }
    public string? DriverName { get; }
    public string? Class { get; }
    public Guid ClassGuid { get; }
    public string? InfName { get; }
    public string? InfOriginalName { get; }
    public string? InfSection { get; }
    public string? InfProvider { get; }
    public string? DriverStoreLocation { get; }

    public Device(
        bool isGeneric,
        string instanceId,
        string? hardwareIds,
        string? description,
        string? friendlyName,
        string? manufacturer,
        string? driverName,
        string? className,
        Guid classGuid,
        string? infName,
        string? infOriginalName,
        string? infSection,
        string? infProvider,
        string? driverStoreLocation)
    {
        IsGeneric = isGeneric;
        InstanceId = instanceId;
        HardwareIds = hardwareIds?.Split('\u0000', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToImmutableArray()
            ?? ImmutableArray<string>.Empty;
        Description = description;
        _friendlyName = friendlyName;
        Manufacturer = manufacturer;
        DriverName = driverName;
        Class = className;
        ClassGuid = classGuid;
        InfName = infName;
        InfOriginalName = infOriginalName;
        InfSection = infSection;
        InfProvider = infProvider;
        DriverStoreLocation = driverStoreLocation;
    }
}
