using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TabletDriverCleanup.Services;
using static Vanara.PInvoke.NewDev;
using static Vanara.PInvoke.SetupAPI;

namespace TabletDriverCleanup.Modules;

public class DeviceCleanupModule : ICleanupModule
{
    private static readonly DeviceSerializerContext _serializerContext = new(
        new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        }
    );

    public static readonly ImmutableArray<DeviceToUninstall> DevicesToUninstall = ImmutableArray.Create(
        new DeviceToUninstall(
            friendlyName: "Wacom Driver Downloader",
            deviceDescription: "Wacom Driver Downloader",
            manufacturerName: "Wacom Technology"),
        new DeviceToUninstall(
            friendlyName: "VMulti Device",
            deviceDescription: "Pentablet HID",
            manufacturerName: "Pentablet HID",
            hardwareId: @"pentablet\\hid",
            classGuid: Guids.HIDClass)
    );

    public string Name { get; } = "Device Cleanup";
    public string CliName { get; } = "device-cleanup";
    public string DisablementDescription => "do not remove devices from the system";
    public bool Enabled { get; set; } = true;

    public bool SupportsDump => true;

    public void Run(ProgramState state)
    {
        var devices = Enumerator.GetDevices();

        foreach (var device in devices)
        {
            if (ShouldUninstall(device, out var deviceToUninstall))
            {
                if (state.Interactive && !state.DryRun)
                {
                    var promptResult = ConsoleUtility.PromptYesNo($"Remove '{deviceToUninstall.FriendlyName}'?");
                    if (promptResult == PromptResult.No)
                        continue;
                    else if (promptResult == PromptResult.Cancel)
                        Environment.Exit(0);
                }
                Console.WriteLine($"Removing '{deviceToUninstall.FriendlyName}'...");

                if (!state.DryRun)
                    RemoveDevice(state, device);
            }
        }
    }

    private static bool ShouldUninstall(Device device, [NotNullWhen(true)] out DeviceToUninstall? deviceToUninstall)
    {
        deviceToUninstall = null;

        foreach (var deviceToUninstallCandidate in DevicesToUninstall)
        {
            if (deviceToUninstallCandidate.DeviceDescriptionRegex.NullableMatch(device.FriendlyName) &&
                deviceToUninstallCandidate.ManufacturerNameRegex.NullableMatch(device.Manufacturer) &&
                deviceToUninstallCandidate.HardwareIdRegex.NullableMatch(device.HardwareId) &&
                (deviceToUninstallCandidate.ClassGuid is not Guid guid || guid == device.ClassGuid))
            {
                deviceToUninstall = deviceToUninstallCandidate;
                return true;
            }
        }

        return false;
    }

    private static void RemoveDevice(ProgramState state, Device device)
    {
        using var deviceInfoSet = SetupDiCreateDeviceInfoList();
        if (deviceInfoSet.IsInvalid)
            throw new Win32Exception();

        var guid = device.ClassGuid;

        SP_DEVINFO_DATA deviceInfoData = new()
        {
            cbSize = (uint)Unsafe.SizeOf<SP_DEVINFO_DATA>()
        };

        if (!SetupDiOpenDeviceInfo(deviceInfoSet, device.InstanceId, 0, 0, ref deviceInfoData))
            throw new Win32Exception();

        if (!DiUninstallDevice(0, deviceInfoSet, in deviceInfoData, 0, out bool rebootRequired))
            throw new Win32Exception();

        if (rebootRequired)
            state.RebootNeeded = true;
    }

    public void Dump(ProgramState state)
    {
        Regex infRegex = Enumerator.InfRegex();
        ImmutableArray<Device> devices = Enumerator.GetDevices()
            .Where(device => infRegex.IsMatch(device.InfName ?? string.Empty))
            .ToImmutableArray();

        using FileStream stream = File.Open(Path.Join(state.CurrentPath, "devices.json"), FileMode.Create, FileAccess.Write);
        JsonSerializer.Serialize(stream, devices, _serializerContext.ImmutableArrayDevice);

        Console.WriteLine($"Dumped {devices.Length} devices to 'devices.json'");
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ImmutableArray<Device>))]
internal partial class DeviceSerializerContext : JsonSerializerContext
{
}