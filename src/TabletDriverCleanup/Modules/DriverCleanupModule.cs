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
        new DriverToUninstall(
            friendlyName: "VMulti",
            originalName: @"vmulti\.inf",
            providerName: "Pentablet HID",
            classGuid: Guids.HIDClass),
        new DriverToUninstall(
            friendlyName: "VMulti",
            originalName: @"vmulti\.inf",
            providerName: "[H|h][U|u][I|i][O|o][N|n]",
            classGuid: Guids.HIDClass),
        new DriverToUninstall(
            friendlyName: "WinUSB (Hawku/Huion)",
            originalName: @"tabletdriver\.inf",
            providerName: "Graphics Tablet",
            classGuid: Guids.USBDevice),
        new DriverToUninstall(
            friendlyName: "WinUSB (Gaomon)",
            originalName: @"winusb\.inf",
            providerName: "Gaomon",
            classGuid: Guids.USBDevice),
        new DriverToUninstall(
            friendlyName: "WinUSB (Huion)",
            originalName: @"winusb\.inf",
            providerName: "Huion",
            classGuid: Guids.USBDevice),
        new DriverToUninstall(
            friendlyName: "WinUSB (libwdi)",
            originalName: @".*",
            providerName: "libwdi",
            classGuid: Guids.USBDevice)
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

        using FileStream stream = File.Open(Path.Join(state.CurrentPath, "drivers.json"), FileMode.Create, FileAccess.Write);
        JsonSerializer.Serialize(stream, drivers, _serializerContext.ImmutableArrayDriver);

        Console.WriteLine($"Dumped {drivers.Length} drivers to 'drivers.json'");
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ImmutableArray<Driver>))]
internal partial class DriverSerializerContext : JsonSerializerContext
{
}