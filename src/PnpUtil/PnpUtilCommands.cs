using System.Diagnostics;

namespace PnpUtil;

// Updated as of: 11/15/2022

/// <summary>
/// PnpUtil is a commandline tool that lets an administrator perform actions on
/// driver packages.
/// </summary>
public static class PnpUtilCommands
{
    private static readonly Version _osVersion = Environment.OSVersion.Version;
    private static readonly Version _10_1607 = new(10, 0, 14393);
    private static readonly Version _10_1903 = new(10, 0, 18362);
    private static readonly Version _10_2004 = new(10, 0, 19041);
    private static readonly Version _11_21H2 = new(10, 0, 22000);
    private static readonly Version _11_22H2 = new(10, 0, 22621);

    /// <summary>
    /// Adds driver package(s) into the driver store. Command available starting
    /// in Windows 10, version 1607.
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="subDirs">Traverse sub directories for driver packages.</param>
    /// <param name="install">Install/update drivers on matching devices.</param>
    /// <param name="reboot">Reboot system if needed to complete the operation.</param>
    /// <returns>A <see cref="PnpUtilReturnCode"/>.</returns>
    public static PnpUtilReturnCode AddDriver(
        string fileName,
        bool subDirs = false,
        bool install = false,
        bool reboot = false)
    {
        ThrowIfUnsupportedCommand(_10_1607, "/add-driver");

        var args = new string[5];
        var itr = 0;

        args[itr++] = "/add-driver";
        args[itr++] = fileName;
        ConditionalFlagAppend(args, ref itr, "/subdirs", subDirs);
        ConditionalFlagAppend(args, ref itr, "/install", install);
        ConditionalFlagAppend(args, ref itr, "/reboot", reboot);

        return InvokePnpUtilCmd(@args, out _);
    }

    /// <summary>
    /// Deletes a driver package from the driver store. Command available starting
    /// in Windows 10, version 1607.
    /// </summary>
    /// <param name="driverName"></param>
    /// <param name="uninstall">uninstall driver package from any devices using it.</param>
    /// <param name="force">delete driver package even when it is in use by devices.</param>
    /// <param name="reboot">reboot system if needed to complete the operation.</param>
    /// <returns>A <see cref="PnpUtilReturnCode"/>.</returns>
    public static PnpUtilReturnCode DeleteDriver(
        string driverName,
        bool uninstall = false,
        bool force = false,
        bool reboot = false
    )
    {
        ThrowIfUnsupportedCommand(_10_1607, "/delete-driver");

        var args = new string[5];
        var itr = 0;

        args[itr++] = "/delete-driver";
        args[itr++] = driverName;
        ConditionalFlagAppend(args, ref itr, "/uninstall", uninstall);
        ConditionalFlagAppend(args, ref itr, "/force", force);
        ConditionalFlagAppend(args, ref itr, "/reboot", reboot);

        return InvokePnpUtilCmd(@args, out _);
    }

    /// <summary>
    /// Exports driver package(s) from the driver store into a target directory.
    /// Command available starting in Windows 10, version 1607.
    /// </summary>
    /// <param name="driverName"></param>
    /// <param name="targetDirectory"></param>
    /// <returns>A <see cref="PnpUtilReturnCode"/>.</returns>
    public static PnpUtilReturnCode ExportDriver(
        string driverName,
        string targetDirectory
    )
    {
        ThrowIfUnsupportedCommand(_10_1607, "/export-driver");

        var args = new string[3];
        var itr = 0;

        args[itr++] = "/export-driver";
        args[itr++] = driverName;
        args[itr++] = targetDirectory;

        return InvokePnpUtilCmd(@args, out _);
    }

    /// <summary>
    /// Enumerates all third-party driver packages in the driver store. Command
    /// available starting in Windows 10, version 1607.
    /// </summary>
    /// <param name="output"></param>
    /// <param name="className">filter by driver class name or GUID (available starting in Windows 11, version 21H2).</param>
    /// <param name="files">enumerate all driver package files (available starting in Windows 11, version 22H2).</param>
    /// <returns>A <see cref="PnpUtilReturnCode"/>.</returns>
    public static PnpUtilReturnCode EnumDrivers(
        out string? output,
        string? className = null,
        bool files = false
    )
    {
        ThrowIfUnsupportedCommand(_10_1607, "/enum-drivers");

        var args = new string[4];
        var itr = 0;

        args[itr++] = "/enum-drivers";
        ConditionalValueAppend(args, ref itr, "/class", className, _11_21H2);
        ConditionalFlagAppend(args, ref itr, "/files", files, _11_22H2);

        return InvokePnpUtilCmd(@args, out output);
    }

    /// <summary>
    /// Disables devices on the system. Command available starting in Windows 10,
    /// version 2004.
    /// </summary>
    /// <param name="instanceId"></param>
    /// <param name="deviceId">disable all devices with matching device ID (available starting in Windows 11, version 21H2).</param>
    /// <param name="className">filter by device class name or GUID (available starting in Windows 11, version 22H2).</param>
    /// <param name="busName">filter by bus enumerator name or bus type GUID (available starting in Windows 11, Version 22H2).</param>
    /// <param name="reboot">reboot system if needed to complete the operation.</param>
    /// <param name="force">disable even if device provides critical system functionality (available starting in Windows 11, Version 22H2).</param>
    /// <returns>A <see cref="PnpUtilReturnCode"/>.</returns>
    /// <exception cref="ArgumentException"></exception>
    public static PnpUtilReturnCode DisableDevice(
        string? instanceId = null,
        string? deviceId = null,
        string? className = null,
        string? busName = null,
        bool reboot = false,
        bool force = false
    )
    {
        ThrowIfUnsupportedCommand(_10_2004, "/disable-device");

        if (instanceId is not null && deviceId is not null)
            throw new ArgumentException($"Cannot specify both {nameof(instanceId)} and {nameof(deviceId)}");

        var args = new string[9];
        var itr = 0;

        args[itr++] = "/disable-device";
        ConditionalAppend(args, ref itr, instanceId);
        ConditionalValueAppend(args, ref itr, "/deviceid", deviceId, _11_21H2);
        ConditionalValueAppend(args, ref itr, "/class", className, _11_22H2);
        ConditionalValueAppend(args, ref itr, "/bus", busName, _11_22H2);
        ConditionalFlagAppend(args, ref itr, "/reboot", reboot);
        ConditionalFlagAppend(args, ref itr, "/force", force, _11_22H2);

        return InvokePnpUtilCmd(@args, out _);
    }

    /// <summary>
    /// Enables devices on the system. Command available starting in Windows 10,
    /// version 2004.
    /// </summary>
    /// <param name="instanceId"></param>
    /// <param name="deviceId">enable all devices with matching device ID (available starting in Windows 11, version 21H2).</param>
    /// <param name="className">filter by device class name or GUID (available starting in Windows 11, version 22H2).</param>
    /// <param name="busName">filter by bus enumerator name or bus type GUID (available starting in Windows 11, Version 22H2).</param>
    /// <param name="reboot">reboot system if needed to complete the operation.</param>
    /// <returns>A <see cref="PnpUtilReturnCode"/>.</returns>
    /// <exception cref="ArgumentException"></exception>
    public static PnpUtilReturnCode EnableDevice(
        string? instanceId = null,
        string? deviceId = null,
        string? className = null,
        string? busName = null,
        bool reboot = false
    )
    {
        ThrowIfUnsupportedCommand(_10_2004, "/enable-device");

        if (instanceId is not null && deviceId is not null)
            throw new ArgumentException($"Cannot specify both {nameof(instanceId)} and {nameof(deviceId)}");

        var args = new string[8];
        var itr = 0;

        args[itr++] = "/enable-device";
        ConditionalAppend(args, ref itr, instanceId);
        ConditionalValueAppend(args, ref itr, "/deviceid", deviceId, _11_21H2);
        ConditionalValueAppend(args, ref itr, "/class", className, _11_22H2);
        ConditionalValueAppend(args, ref itr, "/bus", busName, _11_22H2);
        ConditionalFlagAppend(args, ref itr, "/reboot", reboot);

        return InvokePnpUtilCmd(@args, out _);
    }

    /// <summary>
    /// Restarts devices on the system. Command available starting in Windows 10,
    /// version 2004.
    /// </summary>
    /// <param name="instanceId"></param>
    /// <param name="deviceId">restart all devices with matching device ID (available starting in Windows 11, version 21H2).</param>
    /// <param name="className">filter by device class name or GUID (available starting in Windows 11, version 22H2).</param>
    /// <param name="busName">filter by bus enumerator name or bus type GUID (available starting in Windows 11, version 22H2).</param>
    /// <param name="reboot">reboot system if needed to complete the operation.</param>
    /// <returns>A <see cref="PnpUtilReturnCode"/>.</returns>
    /// <exception cref="ArgumentException"></exception>
    public static PnpUtilReturnCode RestartDevice(
        string? instanceId = null,
        string? deviceId = null,
        string? className = null,
        string? busName = null,
        bool reboot = false
    )
    {
        ThrowIfUnsupportedCommand(_10_2004, "/restart-device");

        if (instanceId is not null && deviceId is not null)
            throw new ArgumentException($"Cannot specify both {nameof(instanceId)} and {nameof(deviceId)}");

        var args = new string[8];
        var itr = 0;

        args[itr++] = "/restart-device";
        ConditionalAppend(args, ref itr, instanceId);
        ConditionalValueAppend(args, ref itr, "/deviceid", deviceId, _11_21H2);
        ConditionalValueAppend(args, ref itr, "/class", className, _11_22H2);
        ConditionalValueAppend(args, ref itr, "/bus", busName, _11_22H2);
        ConditionalFlagAppend(args, ref itr, "/reboot", reboot);

        return InvokePnpUtilCmd(@args, out _);
    }

    /// <summary>
    /// Attempts to remove a device from the system. Command available starting in
    /// Windows 10, version 2004.
    /// </summary>
    /// <param name="instanceId"></param>
    /// <param name="deviceId">remove all devices with matching device ID (available starting in Windows 11, version 21H2).</param>
    /// <param name="className">filter by device class name or GUID (available starting in Windows 11, version 22H2).</param>
    /// <param name="busName">filter by bus enumerator name or bus type GUID (available starting in Windows 11, version 22H2).</param>
    /// <param name="subtree">remove entire device subtree, including any child devices.</param>
    /// <param name="reboot">reboot system if needed to complete the operation.</param>
    /// <param name="force">remove even if device provides critical system functionality (available starting in Windows 11, version 22H2).</param>
    /// <returns>A <see cref="PnpUtilReturnCode"/>.</returns>
    /// <exception cref="ArgumentException"></exception>
    public static PnpUtilReturnCode RemoveDevice(
        string? instanceId = null,
        string? deviceId = null,
        string? className = null,
        string? busName = null,
        bool subtree = false,
        bool reboot = false,
        bool force = false
    )
    {
        ThrowIfUnsupportedCommand(_10_2004, "/remove-device");

        if (instanceId is not null && deviceId is not null)
            throw new ArgumentException($"Cannot specify both {nameof(instanceId)} and {nameof(deviceId)}");

        var args = new string[10];
        var itr = 0;

        args[itr++] = "/remove-device";
        ConditionalAppend(args, ref itr, instanceId);
        ConditionalValueAppend(args, ref itr, "/deviceid", deviceId, _11_21H2);
        ConditionalValueAppend(args, ref itr, "/class", className, _11_22H2);
        ConditionalValueAppend(args, ref itr, "/bus", busName, _11_22H2);
        ConditionalFlagAppend(args, ref itr, "/subtree", subtree);
        ConditionalFlagAppend(args, ref itr, "/reboot", reboot);
        ConditionalFlagAppend(args, ref itr, "/force", force, _11_22H2);

        return InvokePnpUtilCmd(@args, out _);
    }

    /// <summary>
    /// Scans the system for any device hardware changes. Command available starting
    /// in Windows 10, version 2004.
    /// </summary>
    /// <param name="output"></param>
    /// <param name="instanceId">scan device subtree for changes.</param>
    /// <returns>A <see cref="PnpUtilReturnCode"/>.</returns>
    public static PnpUtilReturnCode ScanDevices(
        out string? output,
        string? instanceId = null)
    {
        ThrowIfUnsupportedCommand(_10_2004, "/scan-devices");

        var args = new string[3];
        var itr = 0;

        args[itr++] = "/scan-devices";
        ConditionalValueAppend(args, ref itr, "/instanceid", instanceId);

        return InvokePnpUtilCmd(@args, out output);
    }

    /// <summary>
    /// Enumerate all devices on the system. Command available starting in Windows
    /// 10 version 1903.
    /// </summary>
    /// <param name="output"></param>
    /// <param name="connected">filter by connected devices.</param>
    /// <param name="disconnected">filter by disconnected devices.</param>
    /// <param name="instanceId">filter by device instance ID.</param>
    /// <param name="deviceId">filter by device hardware and compatible ID (available starting in Windows 11, version 22H2).</param>
    /// <param name="className">filter by device class name or GUID.</param>
    /// <param name="problem">filter by devices with problems or filter by specific problem code.</param>
    /// <param name="busName">display bus enumerator name and bus type GUID or filter by bus enumerator name or bus type GUID (available starting in Windows 11, version 21H2).</param>
    /// <param name="deviceIds">display hardware and compatible IDs (available starting in Windows 11, version 21H2).</param>
    /// <param name="relations">display parent and child device relations.</param>
    /// <param name="services">display device services (available starting in Windows 11, version 21H2).</param>
    /// <param name="stack">display effective device stack information (available starting in Windows 11, version 21H2).</param>
    /// <param name="drivers">display matching and installed drivers.</param>
    /// <param name="interfaces">display device interfaces (available starting in Windows 11, version 21H2).</param>
    /// <param name="properties">display all device properties (available starting in Windows 11, version 21H2).</param>
    /// <param name="resources">display device resources (available starting in Windows 11, version 22H2).</param>
    /// <returns>A <see cref="PnpUtilReturnCode"/>.</returns>
    public static PnpUtilReturnCode EnumDevices(
        out string? output,
        bool connected = false,
        bool disconnected = false,
        string? instanceId = null,
        string? deviceId = null,
        string? className = null,
        string? problem = null,
        string? busName = null,
        bool deviceIds = false,
        bool relations = false,
        bool services = false,
        bool stack = false,
        bool drivers = false,
        bool interfaces = false,
        bool properties = false,
        bool resources = false
    )
    {
        ThrowIfUnsupportedCommand(_10_1903, "/enum-devices");

        var args = new string[18];
        var itr = 0;

        args[itr++] = "/enum-devices";
        ConditionalFlagAppend(args, ref itr, "/connected", connected);
        ConditionalFlagAppend(args, ref itr, "/disconnected", disconnected);
        ConditionalValueAppend(args, ref itr, "/instanceid", instanceId);
        ConditionalValueAppend(args, ref itr, "/deviceid", deviceId, _11_22H2);
        ConditionalValueAppend(args, ref itr, "/class", className);
        ConditionalValueAppend(args, ref itr, "/problem", problem);
        ConditionalValueAppend(args, ref itr, "/bus", busName, _11_21H2);
        ConditionalFlagAppend(args, ref itr, "/deviceids", deviceIds, _11_21H2);
        ConditionalFlagAppend(args, ref itr, "/relations", relations);
        ConditionalFlagAppend(args, ref itr, "/services", services, _11_21H2);
        ConditionalFlagAppend(args, ref itr, "/stack", stack, _11_21H2);
        ConditionalFlagAppend(args, ref itr, "/drivers", drivers);
        ConditionalFlagAppend(args, ref itr, "/interfaces", interfaces, _11_21H2);
        ConditionalFlagAppend(args, ref itr, "/properties", properties, _11_21H2);
        ConditionalFlagAppend(args, ref itr, "/resources", resources, _11_22H2);

        return InvokePnpUtilCmd(@args, out output);
    }

    /// <summary>
    /// Enumerates all device interfaces on the system. Command available starting
    /// in Windows 10 version 1903.
    /// </summary>
    /// <param name="output"></param>
    /// <param name="enabled">filter by enabled interfaces.</param>
    /// <param name="disabled">filter by disabled interfaces.</param>
    /// <param name="className">filter by interface class GUID.</param>
    /// <param name="properties">display all interface properties (available starting in Windows 11, version 22H2).</param>
    /// <returns>A <see cref="PnpUtilReturnCode"/>.</returns>
    public static PnpUtilReturnCode EnumInterfaces(
        out string? output,
        bool enabled = false,
        bool disabled = false,
        string? className = null,
        bool properties = false
    )
    {
        ThrowIfUnsupportedCommand(_10_1903, "/enum-interfaces");

        var args = new string[7];
        var itr = 0;

        args[itr++] = "/enum-interfaces";
        ConditionalFlagAppend(args, ref itr, "/enabled", enabled);
        ConditionalFlagAppend(args, ref itr, "/disabled", disabled);
        ConditionalValueAppend(args, ref itr, "/class", className);
        ConditionalFlagAppend(args, ref itr, "/properties", properties, _11_22H2);

        return InvokePnpUtilCmd(@args, out output);
    }

    /// <summary>
    /// Enumerates all device classes on the system. Command available starting
    /// in Windows 11, version 22H2.
    /// </summary>
    /// <param name="output"></param>
    /// <param name="className">filter by device class name or GUID.</param>
    /// <param name="services">display device class services.</param>
    /// <returns>A <see cref="PnpUtilReturnCode"/>.</returns>
    public static PnpUtilReturnCode EnumClasses(
        out string? output,
        string? className = null,
        bool services = false
    )
    {
        ThrowIfUnsupportedCommand(_11_22H2, "/enum-classes");

        var args = new string[4];
        var itr = 0;

        args[itr++] = "/enum-classes";
        ConditionalValueAppend(args, ref itr, "/class", className);
        ConditionalFlagAppend(args, ref itr, "/services", services);

        return InvokePnpUtilCmd(@args, out output);
    }

    public static PnpUtilReturnCode GetHelp(out string? output)
    {
        return InvokePnpUtilCmd(new[] { "/?" }, out output);
    }

    private static void ConditionalAppend(string[] args, ref int itr, string? argument)
    {
        if (argument is not null)
            args[itr++] = argument;
    }

    private static void ConditionalFlagAppend(string[] args, ref int itr, string flag, bool condition)
    {
        if (condition)
            args[itr++] = flag;
    }

    private static void ConditionalFlagAppend(string[] args, ref int itr, string flag, bool condition, Version minimumVersion)
    {
        if (condition)
        {
            ThrowIfUnsupportedFlag(minimumVersion, flag);
            args[itr++] = flag;
        }
    }

    private static void ConditionalValueAppend(string[] args, ref int itr, string @switch, string? value)
    {
        if (value is not null)
        {
            args[itr++] = @switch;
            args[itr++] = value;
        }
    }

    private static void ConditionalValueAppend(string[] args, ref int itr, string @switch, string? value, Version minimumVersion)
    {
        if (value is not null)
        {
            ThrowIfUnsupportedFlag(minimumVersion, @switch);
            args[itr++] = @switch;
            args[itr++] = value;
        }
    }

    private static PnpUtilReturnCode InvokePnpUtilCmd(string?[] arguments, out string? output)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "pnputil.exe",
            Arguments = string.Join(" ", arguments),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        });

        output = process!.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return (PnpUtilReturnCode)process.ExitCode;
    }

    private static void ThrowIfUnsupportedCommand(Version minimumSupportedVersion, string command)
    {
        if (_osVersion < minimumSupportedVersion)
            throw new PlatformNotSupportedException($"Windows {_osVersion} does not support '{command}'. Minimum supported version is {minimumSupportedVersion}.");
    }

    private static void ThrowIfUnsupportedFlag(Version minimumSupportedVersion, string flag)
    {
        if (_osVersion < minimumSupportedVersion)
            throw new PlatformNotSupportedException($"Windows {_osVersion} does not support the flag '{flag}'. Minimum supported version is {minimumSupportedVersion}.");
    }
}
