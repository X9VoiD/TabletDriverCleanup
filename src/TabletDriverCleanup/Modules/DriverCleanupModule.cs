using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using TabletDriverCleanup.Services;
using static Vanara.PInvoke.NewDev;

namespace TabletDriverCleanup.Modules;

public class DriverCleanupModule : ICleanupModule
{
    private static readonly DriverSerializerContext _serializerContext = new(
        new JsonSerializerOptions()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        }
    );

    public static readonly ImmutableArray<DriverToUninstall> DriversToUninstall = ImmutableArray.Create(
        new DriverToUninstall("VMulti", @"vmulti\.inf", "Pentablet HID"),
        new DriverToUninstall("VMulti", @"vmulti\.inf", "[H|h][U|u][I|i][O|o][N|n]"),
        new DriverToUninstall("WinUSB (Hawku/Huion)", @"tabletdriver\.inf", "Graphics Tablet"),
        new DriverToUninstall("WinUSB (Gaomon)", @"winusb\.inf", "Gaomon"),
        new DriverToUninstall("WinUSB (Huion)", @"winusb\.inf", "Huion"),
        new DriverToUninstall("WinUSB (libwdi)", @".*", "libwdi", Guids.USBDevice)
    );

    public string Name { get; } = "Driver Cleanup";
    public string CliName { get; } = "driver-cleanup";
    public string DisablementDescription { get; } = "do not uninstall drivers from the system";
    public bool Enabled { get; set; } = true;

    public bool SupportsDump => true;

    public void Run(ProgramState state)
    {
        var drivers = Enumerator.GetDrivers();

        foreach (var driver in drivers)
        {
            if (ShouldUninstall(driver, out var driverToUninstall))
            {
                if (state.Interactive && !state.DryRun)
                {
                    var promptResult = ConsoleUtility.PromptYesNo($"Uninstall '{driverToUninstall.FriendlyName}'?");
                    if (promptResult == PromptResult.No)
                        continue;
                    else if (promptResult == PromptResult.Cancel)
                        Environment.Exit(0);
                }
                Console.WriteLine($"Uninstalling '{driverToUninstall.FriendlyName}'...");

                if (!state.DryRun)
                    UninstallDriver(state, driver);
            }
        }
    }

    private static bool ShouldUninstall(Driver driver, [NotNullWhen(true)] out DriverToUninstall? driverToUninstall)
    {
        driverToUninstall = null;

        foreach (var driverToUninstallCandidate in DriversToUninstall)
        {
            if (driverToUninstallCandidate.OriginalNameRegex.NullableMatch(driver.InfOriginalName) &&
                driverToUninstallCandidate.ProviderNameRegex.NullableMatch(driver.Provider) &&
                (driverToUninstallCandidate.ClassGuid is not Guid guid || guid == driver.ClassGuid))
            {
                driverToUninstall = driverToUninstallCandidate;
                return true;
            }
        }

        return false;
    }

    private static void UninstallDriver(ProgramState state, Driver driver)
    {
        var infPath = Path.Join(driver.DriverStoreLocation, driver.InfOriginalName);
        if (!DiUninstallDriver(0, infPath, 0, out bool rebootRequired))
            throw new Win32Exception();

        if (rebootRequired)
            state.RebootNeeded = true;
    }

    public void Dump(ProgramState state)
    {
        ImmutableArray<Driver> drivers = Enumerator.GetDrivers();

        using FileStream stream = File.OpenWrite(Path.Join(state.CurrentPath, "drivers.json"));
        JsonSerializer.Serialize(stream, drivers, _serializerContext.ImmutableArrayDriver);

        Console.WriteLine($"Dumped {drivers.Length} drivers to 'drivers.json'");
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ImmutableArray<Driver>))]
internal partial class DriverSerializerContext : JsonSerializerContext
{
}