using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TabletDriverCleanup.Services;
using static Vanara.PInvoke.NewDev;
using static Vanara.PInvoke.SetupAPI;

namespace TabletDriverCleanup.Modules;

public class DeviceCleanupModule : BaseCleanupModule<Device, DeviceToUninstall>
{
    private const string DEVICE_CONFIG = "device_identifiers.json";

    private static readonly DeviceSerializerContext _serializerContext = new(
        new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
        }
    );

    public override string Name => "Device Cleanup";
    public override string CliName => "device-cleanup";
    public override string DisablementDescription => "do not remove devices from the system";

    public override bool SupportsDump => true;

    protected override string Noun => "devices";

    public override void Dump(ProgramState state)
    {
        Regex infRegex = Enumerator.InfRegex();
        ImmutableArray<Device> devices = Enumerator.GetDevices()
            .Where(device => infRegex.IsMatch(device.InfName ?? string.Empty))
            .Where(IsOfInterest)
            .ToImmutableArray();

        using var stream = GetDumpFileStream(state, "devices.json");
        if (devices.Length == 0)
        {
            Console.WriteLine("No devices to dump");
            return;
        }
        JsonSerializer.Serialize(stream, devices, _serializerContext.ImmutableArrayDevice);

        Console.WriteLine($"Dumped {devices.Length} devices to 'devices.json'");
    }

    private bool IsOfInterest(Device arg)
    {
        return StringOfInterest.IsCandidate(
            arg.Description,
            arg.Manufacturer,
            arg.InfOriginalName)
        || StringOfInterest.IsCandidate(arg.HardwareIds);
    }

    protected override IEnumerable<Device> GetObjects(ProgramState state)
    {
        return Enumerator.GetDevices();
    }

    protected override ImmutableArray<DeviceToUninstall> GetObjectsToUninstall(ProgramState state)
    {
        var devicesConfig = state.ConfigurationManager[DEVICE_CONFIG];
        return JsonSerializer.Deserialize(devicesConfig, _serializerContext.ImmutableArrayDeviceToUninstall);
    }

    protected override void UninstallObject(ProgramState state, Device device, DeviceToUninstall deviceToUninstall)
    {
        using var deviceInfoSet = SetupDiCreateDeviceInfoList();
        if (deviceInfoSet.IsInvalid)
            throw new Win32Exception();

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
}

[JsonSerializable(typeof(ImmutableArray<Device>))]
[JsonSerializable(typeof(ImmutableArray<DeviceToUninstall>))]
internal partial class DeviceSerializerContext : JsonSerializerContext
{
}