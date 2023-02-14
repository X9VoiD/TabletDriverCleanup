using System.Collections.Immutable;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using TabletDriverCleanup.Services;
using static Vanara.PInvoke.NewDev;

namespace TabletDriverCleanup.Modules;

public class DriverCleanupModule : BaseCleanupModule<Driver, DriverToUninstall>
{
    private const string DRIVER_CONFIG = "driver_identifiers.json";

    private static readonly DriverSerializerContext _serializerContext = new(
        new JsonSerializerOptions()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
        }
    );

    public override string Name => "Driver Cleanup";
    public override string CliName => "driver-cleanup";
    public override string DisablementDescription => "do not uninstall drivers from the system";

    public override bool SupportsDump => true;

    protected override string Noun => "drivers";

    public override void Dump(ProgramState state)
    {
        ImmutableArray<Driver> drivers = Enumerator.GetDrivers()
            .Where(IsOfInterest)
            .ToImmutableArray();

        using var stream = GetDumpFileStream(state, "drivers.json");
        if (drivers.Length == 0)
        {
            Console.WriteLine("No drivers to dump");
            return;
        }
        JsonSerializer.Serialize(stream, drivers, _serializerContext.ImmutableArrayDriver);

        Console.WriteLine($"Dumped {drivers.Length} drivers to 'drivers.json'");
    }

    private bool IsOfInterest(Driver arg)
    {
        return StringOfInterest.IsCandidate(
            arg.InfOriginalName,
            arg.Provider);
    }

    protected override IEnumerable<Driver> GetObjects(ProgramState state)
    {
        return Enumerator.GetDrivers();
    }

    protected override ImmutableArray<DriverToUninstall> GetObjectsToUninstall(ProgramState state)
    {
        var driverConfig = state.ConfigurationManager[DRIVER_CONFIG];
        return JsonSerializer.Deserialize(driverConfig, _serializerContext.ImmutableArrayDriverToUninstall)!;
    }

    protected override void UninstallObject(ProgramState state, Driver driver, DriverToUninstall driverToUninstall)
    {
        var infPath = Path.Join(driver.DriverStoreLocation, driver.InfOriginalName);
        if (!DiUninstallDriver(0, infPath, 0, out bool rebootRequired))
            throw new Win32Exception();

        if (rebootRequired)
            state.RebootNeeded = true;
    }
}

[JsonSerializable(typeof(ImmutableArray<Driver>))]
[JsonSerializable(typeof(ImmutableArray<DriverToUninstall>))]
internal partial class DriverSerializerContext : JsonSerializerContext
{
}