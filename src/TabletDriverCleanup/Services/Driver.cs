using System.Diagnostics;

namespace TabletDriverCleanup.Services;

[DebuggerDisplay($"{{{nameof(Provider)}}} - {{{nameof(InfOriginalName)}}}")]
public class Driver
{
    public string? InfName { get; }
    public string? InfOriginalName { get; }
    public string? DriverStoreLocation { get; }
    public string? Provider { get; }
    public string? Class { get; }
    public Guid ClassGuid { get; }

    public Driver(
        string? infName,
        string? infOriginalName,
        string? driverStoreLocation,
        string? provider,
        string? className,
        Guid classGuid)
    {
        InfName = infName;
        InfOriginalName = infOriginalName;
        DriverStoreLocation = driverStoreLocation;
        Provider = provider;
        Class = className;
        ClassGuid = classGuid;
    }
}