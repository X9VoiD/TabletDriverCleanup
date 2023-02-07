using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text.RegularExpressions;
using PnpUtil;

namespace TabletDriverCleanup;

public static partial class Program
{
    public static bool NoDriverUninstall { get; set; }
    public static bool NoDeviceUninstall { get; set; }
    public static bool NoWarning { get; set; }
    public static bool DryRun { get; set; }
    public static bool RebootNeeded { get; set; }

    public static readonly ImmutableArray<DriverInfToUninstall> DriversToUninstall = ImmutableArray.Create(
        new DriverInfToUninstall("VMulti", @"vmulti\.inf", "Pentablet HID"),
        new DriverInfToUninstall("VMulti", @"vmulti\.inf", "[H|h][U|u][I|i][O|o][N|n]"),
        new DriverInfToUninstall("WinUSB (Gaomon)", @"winusb\.inf", "Gaomon"),
        new DriverInfToUninstall("WinUSB (Wacom)", @"winusb\.inf", "Wacom"),
        new DriverInfToUninstall("WinUSB (Huion)", @"winusb\.inf", "Huion"),
        new DriverInfToUninstall("WinUSB (libwdi)", @".*", "libwdi", "USBDevice"),
        new DriverInfToUninstall("WinUSB (libwdi)", @".*", "libwdi", "Universal Serial Bus devices")
    );

    public static readonly ImmutableArray<DeviceToUninstall> DevicesToUninstall = ImmutableArray.Create(
        new DeviceToUninstall("Wacom Driver Downloader", "Wacom Driver Downloader", manufacturerName: "Wacom Technology"),
        new DeviceToUninstall("VMulti Device", "Pentablet HID", manufacturerName: "Pentablet HID")
    );

    public static readonly ImmutableArray<HeuristicSample> Heuristics = ImmutableArray.Create(
        new HeuristicSample("Gaomon"),
        new HeuristicSample("Huion"),
        new HeuristicSample("Wacom"),
        new HeuristicSample("Veikk")
    );

    [SupportedOSPlatform("windows")]
    public static int Main(string[] args)
    {
        CheckAdmin();
        Console.WriteLine("TabletDriverCleanup\n");

        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--no-driver-uninstall":
                    NoDriverUninstall = true;
                    break;
                case "--no-device-uninstall":
                    NoDeviceUninstall = true;
                    break;
                case "--yes" or "-y":
                    NoWarning = true;
                    break;
                case "--dry-run":
                    DryRun = true;
                    break;
                case "--help":
                    PrintHelp();
                    return 0;
                default:
                    Console.Error.WriteLine($"Unknown argument: {arg}");
                    PrintHelp();
                    return 1;
            }
        }

        if (DryRun)
            Console.WriteLine("Dry run, no changes will be made.");

        if (!DryRun && !NoWarning)
        {
            Console.WriteLine("Make sure that all tablet drivers have been uninstalled via their official uninstallers.");
            Console.WriteLine("Once done, press Enter to continue...");
            Console.ReadKey();
            Console.WriteLine();
        }

        if (!NoDeviceUninstall)
            UninstallDevices();

        Console.WriteLine();
        if (!NoDriverUninstall)
            UninstallDrivers();

        if (RebootNeeded)
        {
            if (NoWarning)
            {
                Console.WriteLine("\nReboot is required to complete the cleanup.");
                Console.WriteLine("Press Enter to reboot now, or Ctrl+C to cancel.");
                Console.ReadKey();
            }
            Process.Start("shutdown", "/r /t 0");
            Environment.Exit(0);
        }

        if (!NoWarning)
        {
            Console.WriteLine("\nPress Enter to continue...");
            Console.ReadKey();
        }

        return 0;
    }

    [SupportedOSPlatform("windows")]
    private static void CheckAdmin()
    {
        WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
        {
            Console.Error.WriteLine("This program must be run as administrator.");
            Console.WriteLine("\nPress Enter to continue...");
            Console.ReadKey();
            Environment.Exit(1);
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Usage: TabletDriverCleanup [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  --no-driver-uninstall\t\tdo not uninstall drivers");
        Console.WriteLine("  --no-device-uninstall\t\tdo not uninstall devices");
        Console.WriteLine("  --dry-run\t\t\tonly print the changes that would be made");
        Console.WriteLine("  --help\t\t\tshow this help message");
    }

    private static void UninstallDevices()
    {
        Console.WriteLine("Uninstalling devices...");
        var success = PnpUtilCommands.EnumDevices(out var output, connected: true);
        if (success != PnpUtilReturnCode.SUCCESS)
        {
            Console.Error.WriteLine($"Failed to enumerate devices: {success}");
            Environment.Exit((int)success);
        }

        var devices = IPnpUtilParseable<PnpUtilDevice>.ParseEnumerable(output!).ToArray();

        var found = false;
        foreach (var device in devices)
        {
            if (!ShouldUninstall(device, out var deviceToUninstall))
                continue;

            found = true;

            if (DryRun)
            {
                Console.WriteLine($"Would uninstall {deviceToUninstall.FriendlyName} ({device.InstanceID})");
                continue;
            }

            var uninstallSuccess = PnpUtilCommands.RemoveDevice(device.InstanceID, subtree: true);
            if (uninstallSuccess == PnpUtilReturnCode.ERROR_SUCCESS_REBOOT_REQUIRED)
            {
                RebootNeeded = true;
            }
            else if (uninstallSuccess != PnpUtilReturnCode.SUCCESS)
            {
                Console.Error.WriteLine($"Failed to uninstall {deviceToUninstall.FriendlyName} ({device.InstanceID}): Error: {uninstallSuccess}");
                continue;
            }

            Console.WriteLine($"Uninstalled {deviceToUninstall.FriendlyName} ({device.InstanceID})");
        }

        if (!found)
            Console.WriteLine("No devices to uninstall.");
    }

    private static void UninstallDrivers()
    {
        Console.WriteLine("Uninstalling drivers...");
        var success = PnpUtilCommands.EnumDrivers(out var output);
        if (success != PnpUtilReturnCode.SUCCESS)
        {
            Console.Error.WriteLine($"Failed to enumerate drivers: {success}");
            Environment.Exit((int)success);
        }

        var drivers = IPnpUtilParseable<PnpUtilDriver>.ParseEnumerable(output!).ToArray();

        var found = false;
        foreach (var driver in drivers)
        {
            if (!ShouldUninstall(driver, out var driverToUninstall))
                continue;

            found = true;

            if (DryRun)
            {
                Console.WriteLine($"Would uninstall {driverToUninstall.FriendlyName} ({driver.OriginalName})");
                continue;
            }

            var uninstallSuccess = PnpUtilCommands.DeleteDriver(driver.DriverName, uninstall: true, force: true);
            if (uninstallSuccess == PnpUtilReturnCode.ERROR_SUCCESS_REBOOT_REQUIRED)
            {
                RebootNeeded = true;
            }
            else if (uninstallSuccess != PnpUtilReturnCode.SUCCESS)
            {
                Console.Error.WriteLine($"Failed to uninstall {driverToUninstall.FriendlyName} ({driver.OriginalName}): Error: {uninstallSuccess}");
                continue;
            }

            Console.WriteLine($"Uninstalled {driverToUninstall.FriendlyName} ({driver.OriginalName})");
        }

        if (!found)
            Console.WriteLine("No drivers to uninstall.");
    }

    private static bool ShouldUninstall(PnpUtilDriver pnpUtilDriver, [NotNullWhen(true)] out DriverInfToUninstall? driverToUninstallInfo)
    {
        driverToUninstallInfo = null;
        foreach (var driverToUninstall in DriversToUninstall)
        {
            if (!RegexMatch(driverToUninstall.OriginalNameRegex, pnpUtilDriver.OriginalName))
                continue;

            if (!RegexMatch(driverToUninstall.ProviderNameRegex, pnpUtilDriver.ProviderName))
                continue;

            if (!RegexMatch(driverToUninstall.ClassNameRegex, pnpUtilDriver.ClassName))
                continue;

            driverToUninstallInfo = driverToUninstall;
            return true;
        }

        return false;
    }

    private static bool ShouldUninstall(PnpUtilDevice pnpUtilDevice, [NotNullWhen(true)] out DeviceToUninstall? deviceToUninstallInfo)
    {
        deviceToUninstallInfo = null;

        foreach (var deviceToUninstall in DevicesToUninstall)
        {
            if (!RegexMatch(deviceToUninstall.DeviceDescriptionRegex, pnpUtilDevice.DeviceDescription))
                continue;

            if (!RegexMatch(deviceToUninstall.ClassNameRegex, pnpUtilDevice.ClassName))
                continue;

            if (!RegexMatch(deviceToUninstall.ManufacturerNameRegex, pnpUtilDevice.ManufacturerName))
                continue;

            deviceToUninstallInfo = deviceToUninstall;
            return true;
        }

        return false;
    }

    private static bool RegexMatch(Regex? regex, string? property)
    {
        if (regex is null)
            return true;

        return regex.IsMatch(property ?? "");
    }

    public static Regex ToRegex(this string str) => new(str);
}