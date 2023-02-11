using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Vanara.PInvoke;
using static Vanara.PInvoke.CfgMgr32;
using static Vanara.PInvoke.SetupAPI;

namespace TabletDriverCleanup.Services;

public static partial class Enumerator
{
    public static ImmutableArray<Device> GetDevices()
    {
        using SafeHDEVINFO deviceInfoSet = SetupDiGetClassDevs((nint)null, null, HWND.NULL, DIGCF.DIGCF_ALLCLASSES | DIGCF.DIGCF_PRESENT);
        if (deviceInfoSet.IsInvalid)
            throw new Win32Exception();

        SP_DEVINFO_DATA deviceInfo = new()
        {
            cbSize = (uint)Unsafe.SizeOf<SP_DEVINFO_DATA>()
        };

        ImmutableArray<Device>.Builder devices = ImmutableArray.CreateBuilder<Device>();

        for (uint i = 0; ; i++)
        {
            if (!SetupDiEnumDeviceInfo(deviceInfoSet, i, ref deviceInfo))
            {
                if (Marshal.GetLastWin32Error() != Win32Error.ERROR_NO_MORE_ITEMS)
                    throw new Win32Exception();

                break;
            }

            bool generic = GetDeviceProperty(deviceInfoSet, in deviceInfo, in DEVPKEY_Device_GenericDriverInstalled, ParseBool);
            string instanceId = GetDeviceInstanceId(deviceInfoSet, in deviceInfo);
            string hardwareId = GetDeviceRegistryProperty(deviceInfoSet, in deviceInfo, SPDRP.SPDRP_HARDWAREID, ParseString)!;
            string? description = GetDeviceRegistryProperty(deviceInfoSet, in deviceInfo, SPDRP.SPDRP_DEVICEDESC, ParseString);
            string? friendlyName = GetDeviceRegistryProperty(deviceInfoSet, in deviceInfo, SPDRP.SPDRP_FRIENDLYNAME, ParseString);
            string? manufacturer = GetDeviceRegistryProperty(deviceInfoSet, in deviceInfo, SPDRP.SPDRP_MFG, ParseString);
            string? driverName = GetDeviceRegistryProperty(deviceInfoSet, in deviceInfo, SPDRP.SPDRP_DRIVER, ParseString);
            string? className = GetDeviceRegistryProperty(deviceInfoSet, in deviceInfo, SPDRP.SPDRP_CLASS, ParseString);
            Guid classGuid = GetDeviceRegistryProperty(deviceInfoSet, in deviceInfo, SPDRP.SPDRP_CLASSGUID, ParseGuid);
            string? infName = GetDeviceProperty(deviceInfoSet, in deviceInfo, in DEVPKEY_Device_DriverInfPath, ParseString);
            string? infOriginalName = GetInfDriverStoreLocation(infName);
            string? infSection = GetDeviceProperty(deviceInfoSet, in deviceInfo, in DEVPKEY_Device_DriverInfSection, ParseString);
            string? infProvider = GetDeviceProperty(deviceInfoSet, in deviceInfo, in DEVPKEY_Device_DriverProvider, ParseString);

            Device device = new(
                isGeneric: generic,
                instanceId: instanceId,
                hardwareId: hardwareId,
                description: description,
                friendlyName: friendlyName,
                manufacturer: manufacturer,
                driverName: driverName,
                className: className,
                classGuid: classGuid,
                infName: infName,
                infOriginalName: Path.GetFileName(infOriginalName),
                infSection: infSection,
                infProvider: infProvider,
                driverStoreLocation: Path.GetDirectoryName(infOriginalName));

            devices.Add(device);
        }

        return devices.ToImmutable();
    }

    public static ImmutableArray<Driver> GetDrivers()
    {
        string[] infList = GetInfFileList();
        ImmutableArray<Driver>.Builder driverBuilder = ImmutableArray.CreateBuilder<Driver>(infList.Length);

        foreach (var inf in infList)
        {
            using SafeHINF infFile = SetupOpenInfFile(inf, null, INF_STYLE.INF_STYLE_OLDNT | INF_STYLE.INF_STYLE_WIN4, out _);
            if (infFile.IsInvalid)
                throw new Win32Exception();

            string? infOriginalName = GetInfDriverStoreLocation(inf);
            string? infProvider = GetInfProperty(infFile, "Version", "Provider");
            string? className = GetInfProperty(infFile, "Version", "Class");
            Guid classGuid = Guid.Parse(GetInfProperty(infFile, "Version", "ClassGUID")!);

            Driver driver = new(
                infName: inf,
                infOriginalName: Path.GetFileName(infOriginalName),
                driverStoreLocation: Path.GetDirectoryName(infOriginalName),
                provider: infProvider,
                className: className,
                classGuid: classGuid);

            driverBuilder.Add(driver);
        }

        return driverBuilder.ToImmutable();
    }

    private static string GetDeviceInstanceId(SafeHDEVINFO deviceInfoSet, in SP_DEVINFO_DATA deviceInfo)
    {
        if (!SetupDiGetDeviceInstanceId(deviceInfoSet, in deviceInfo, null, 0, out uint requiredSize))
        {
            if ((uint)Marshal.GetLastWin32Error() != Win32Error.ERROR_INSUFFICIENT_BUFFER)
                throw new Win32Exception();
        }

        StringBuilder instanceId = new((int)requiredSize);

        if (!SetupDiGetDeviceInstanceId(deviceInfoSet, in deviceInfo, instanceId, requiredSize, out _))
            throw new Win32Exception();

        return instanceId.ToString();
    }

    private static unsafe T? GetDeviceRegistryProperty<T>(SafeHDEVINFO deviceInfoSet, in SP_DEVINFO_DATA deviceInfo, SPDRP property, ParsePropertyDelegate<T> parser)
    {
        bool nothing = false;
        return GenericGet((deviceInfoSet, property), in deviceInfo, in nothing,
            getter, parser, stackalloc uint[] { Win32Error.ERROR_INVALID_DATA });

        static bool getter((SafeHDEVINFO, SPDRP) deviceInfoSet, in SP_DEVINFO_DATA deviceInfo, in bool nothing, nint buffer, uint bufferSize, out uint output)
        {
            return SetupDiGetDeviceRegistryProperty(deviceInfoSet.Item1, in deviceInfo, deviceInfoSet.Item2, out _, buffer, bufferSize, out output);
        };
    }

    private static unsafe T? GetDeviceProperty<T>(SafeHDEVINFO deviceInfoSet, in SP_DEVINFO_DATA deviceInfo, in DEVPROPKEY propertyKey, ParsePropertyDelegate<T> parser)
    {
        return GenericGet(deviceInfoSet, in deviceInfo, in propertyKey,
            getter, parser, stackalloc uint[] { Win32Error.ERROR_NOT_FOUND });

        static bool getter(SafeHDEVINFO deviceInfoSet, in SP_DEVINFO_DATA deviceInfo, in DEVPROPKEY propertyKey, nint buffer, uint bufferSize, out uint output)
        {
            return SetupDiGetDeviceProperty(deviceInfoSet, in deviceInfo, in propertyKey, out _, buffer, bufferSize, out output, 0);
        };
    }

    private static unsafe string? GetInfDriverStoreLocation(string? infName)
    {
        if (string.IsNullOrWhiteSpace(infName))
            return null;

        if (!SetupGetInfDriverStoreLocation(infName, 0, null, null, 0, out uint size))
        {
            if ((uint)Marshal.GetLastWin32Error() != Win32Error.ERROR_INSUFFICIENT_BUFFER)
                throw new Win32Exception();
        }

        StringBuilder builder = new((int)size);

        if (!SetupGetInfDriverStoreLocation(infName, 0, null, builder, size, out _))
            throw new Win32Exception();

        return builder.ToString();
    }

    private static string? GetInfProperty(SafeHINF infFile, string section, string key)
    {
        if (!SetupGetLineText(0, infFile, section, key, null, 0, out uint size))
        {
            if ((uint)Marshal.GetLastWin32Error() != Win32Error.ERROR_INSUFFICIENT_BUFFER)
                throw new Win32Exception();
        }

        StringBuilder builder = new((int)size);

        if (!SetupGetLineText(0, infFile, section, key, builder, size, out _))
            throw new Win32Exception();

        return builder.ToString();
    }

    private static string[] GetInfFileList()
    {
        var windir = Environment.GetEnvironmentVariable("windir");
        var infRegex = InfRegex();
        return Directory.EnumerateFiles(Path.Join(windir, "inf"), "*.inf", SearchOption.AllDirectories)
            .Select(f => Path.GetFileName(f))
            .Where(f => infRegex.IsMatch(f))
            .ToArray();
    }

    private static unsafe TReturn? GenericGet<TInput, TInput2, TInput3, TReturn>(
        TInput input,
        in TInput2 input2,
        in TInput3 input3,
        GenericGetterDelegate<TInput, TInput2, TInput3> getter,
        ParsePropertyDelegate<TReturn> parser,
        ReadOnlySpan<uint> skipCodes)
    {
        if (!getter(input, in input2, in input3, 0, 0, out uint size))
        {
            var err = (uint)Marshal.GetLastWin32Error();
            if (skipCodes.Contains(err))
                return default;

            if (err != Win32Error.ERROR_INSUFFICIENT_BUFFER)
                throw new Win32Exception();
        }

        byte[]? managedBuffer = null;
        Span<byte> buffer = size < 2048
            ? stackalloc byte[(int)size]
            : managedBuffer = new byte[size];

        // Clear the buffer if it was allocated from the stack
        if (managedBuffer == null)
            buffer.Clear();

        fixed (byte* bufferPtr = buffer)
        {
            if (!getter(input, in input2, in input3, (nint)bufferPtr, size, out size))
                throw new Win32Exception();
        }

        return parser(buffer);
    }

    private static Guid ParseGuid(ReadOnlySpan<byte> buffer)
    {
        string? guidString = ParseString(buffer);
        return guidString != null ? Guid.Parse(guidString) : Guid.Empty;
    }

    private static bool ParseBool(ReadOnlySpan<byte> buffer)
    {
        sbyte value = (sbyte)buffer[0];
        return value == -1;
    }

    private static string? ParseString(ReadOnlySpan<byte> buffer)
    {
        buffer = buffer[..^2]; // remove null terminator
        return Encoding.Unicode.GetString(buffer);
    }

    [GeneratedRegex(@"^oem[0-9]+\.inf$")]
    private static partial Regex InfRegex();

    private delegate T ParsePropertyDelegate<T>(ReadOnlySpan<byte> buffer);
    private delegate bool GenericGetterDelegate<TInput, TInput2, TInput3>(TInput input, in TInput2 input2, in TInput3 input3, nint buffer, uint bufferSize, out uint requiredSize);
}
