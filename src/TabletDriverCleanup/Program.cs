using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;
using TabletDriverCleanup.Modules;

namespace TabletDriverCleanup;

public static partial class Program
{
    private static ICleanupModule[] CreateModules()
    {
        return new ICleanupModule[]
        {
            // new DriverSoftwareCleanupModule(),
            new DeviceCleanupModule(),
            new DriverCleanupModule(),
        };
    }

    [SupportedOSPlatform("windows")]
    public static int Main(string[] args)
    {
        var modules = CreateModules();

        var state = new ProgramState(modules);
        ParseCliArgs(state, args);

        Console.WriteLine("TabletDriverCleanup\n");

        if (state.Dump)
        {
            Console.WriteLine("Dumping...");
            foreach (var module in modules.Where(m => m.Enabled && m.SupportsDump))
                module.Dump(state);
            return 0;
        }

        if (!state.DryRun)
            CheckAdmin();

        if (state.DryRun)
            Console.WriteLine("Dry run, no changes will be made.\n");

        if (!state.DryRun && state.Interactive)
        {
            Console.WriteLine("Make sure that all tablet drivers have been uninstalled via their official uninstallers.");
            Console.WriteLine("Once done, press Enter to continue...");
            Console.ReadKey();
            Console.WriteLine();
        }

        foreach (var module in modules.Where(m => m.Enabled))
        {
            Console.WriteLine($"Running '{module.Name}'...");

            try
            {
                module.Run(state);
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Console.WriteLine($"\nErrors were encountered while running '{module.Name}'. Aborting!");

                if (state.Interactive)
                {
                    Console.WriteLine("Press Enter to continue...");
                    Console.ReadKey();
                }

                Environment.Exit(1);
            }
        }

        if (state.RebootNeeded)
        {
            if (state.Interactive)
            {
                Console.WriteLine("\nReboot is required to complete the cleanup.");
                Console.WriteLine("Press Enter to reboot now, or Ctrl+C to cancel.");
                Console.ReadKey();
            }
            Process.Start("shutdown", "/r /t 0");
            Environment.Exit(0);
        }

        if (state.Interactive)
        {
            Console.WriteLine("\nCleanup complete. Press Enter to continue...");
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

    private static void ParseCliArgs(ProgramState state, string[] args)
    {
        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--dump":
                    state.Dump = true;
                    break;

                case "--no-prompt":
                    state.Interactive = false;
                    break;

                case "--no-cache":
                    state.NoCache = true;
                    break;

                case string when arg.StartsWith("--no-"):
                    var moduleName = arg.AsSpan()[5..];

                    var found = false;
                    foreach (var module in state.Modules)
                    {
                        if (module.CliName == moduleName)
                        {
                            module.Enabled = false;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        ThrowInvalidArg(arg);
                    break;

                case "--dry-run":
                    state.DryRun = true;
                    break;

                case "--help":
                case "-h":
                    PrintHelp(state);
                    Environment.Exit(0);
                    break;

                default:
                    ThrowInvalidArg(arg);
                    break;
            }
        }

        return;

    }

    private static void PrintHelp(ProgramState state)
    {
        Console.WriteLine("Usage: TabletDriverCleanup [options]");
        Console.WriteLine("Options:");

        Console.WriteLine("  --no-prompt\t\t\tdo not prompt for user input");
        Console.WriteLine("  --no-cache\t\t\tdo not use cached data in ./config");

        foreach (var module in state.Modules)
            Console.WriteLine($"  --no-{module.CliName}\t\t{module.DisablementDescription}");

        Console.WriteLine("  --dry-run\t\t\tonly print the changes that would be made");
        Console.WriteLine("  --dump\t\t\tdump some information about devices and drivers");
        Console.WriteLine("  --help\t\t\tshow this help message");
    }

    private static void ThrowInvalidArg(string arg)
    {
        Console.WriteLine($"Invalid argument: '{arg}'");
        Environment.Exit(1);
    }
}