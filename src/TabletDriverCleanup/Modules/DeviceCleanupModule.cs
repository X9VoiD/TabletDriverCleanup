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
    private const string DEVICE_CONFIG = "device_identifiers.json";

    private static readonly DeviceSerializerContext _serializerContext = new(
        new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
        }
    );

    private ImmutableArray<DeviceToUninstall> _devicesToUninstall;
    private readonly RegexCache _regexCache = new();

    public string Name { get; } = "Device Cleanup";
    public string CliName { get; } = "device-cleanup";
    public string DisablementDescription => "do not remove devices from the system";
    public bool Enabled { get; set; } = true;

    public bool SupportsDump => true;

    public void Run(ProgramState state)
    {
        var devices = Enumerator.GetDevices();
        var devicesConfig = state.ConfigurationManager[DEVICE_CONFIG];
        _devicesToUninstall = JsonSerializer.Deserialize(devicesConfig, _serializerContext.ImmutableArrayDeviceToUninstall);

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

    private bool ShouldUninstall(Device device, [NotNullWhen(true)] out DeviceToUninstall? deviceToUninstall)
    {
        deviceToUninstall = null;

        foreach (var deviceToUninstallCandidate in _devicesToUninstall)
        {
            Regex deviceDescriptionRegex = _regexCache.GetRegex(deviceToUninstallCandidate.DeviceDescription);
            Regex? manufacturerNameRegex = _regexCache.GetRegex(deviceToUninstallCandidate.ManufacturerName);
            Regex? hardwareIdRegex = _regexCache.GetRegex(deviceToUninstallCandidate.HardwareId);

            if (deviceDescriptionRegex.NullableMatch(device.FriendlyName) &&
                manufacturerNameRegex.NullableMatch(device.Manufacturer) &&
                hardwareIdRegex.NullableMatch(device.HardwareIds) &&
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

[JsonSerializable(typeof(ImmutableArray<Device>))]
[JsonSerializable(typeof(ImmutableArray<DeviceToUninstall>))]
internal partial class DeviceSerializerContext : JsonSerializerContext
{
}