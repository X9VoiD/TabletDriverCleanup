using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Runtime.ExceptionServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using TabletDriverCleanup.Services;
using Vanara.PInvoke;

namespace TabletDriverCleanup.Modules;

public partial class DriverPackageCleanupModule : BaseCleanupModule<DriverPackage, DriverPackageToUninstall>
{
    private const string DRIVER_PACKAGE_CONFIG = "driver_package_identifiers.json";

    private static readonly DriverPackageSerializerContext _serializerContext = new(
        new JsonSerializerOptions()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
        }
    );

    public override string Name => "Driver Package Cleanup";
    public override string CliName => "driver-package-cleanup";
    public override string DisablementDescription => "do not remove driver packages from the system";

    public override bool SupportsDump => true;

    protected override string Noun => "driver packages";

    [SupportedOSPlatform("windows")]
    public override void Dump(ProgramState state)
    {
        var driverPackages = Enumerator.GetDriverPackages()
            .Where(IsOfInterest)
            .ToImmutableArray();

        using var stream = GetDumpFileStream(state, "driver_packages.json");
        if (driverPackages.Length == 0)
        {
            Console.WriteLine("No driver packages to dump");
            return;
        }
        JsonSerializer.Serialize(stream, driverPackages, _serializerContext.ImmutableArrayDriverPackage);

        Console.WriteLine($"Dumped {driverPackages.Length} driver packages to 'driver_packages.json'");
    }

    private bool IsOfInterest(DriverPackage arg)
    {
        return arg.DisplayName is not null
            && arg.UninstallString is not null
            && StringOfInterest.IsCandidate(
                arg.DisplayName,
                arg.Publisher,
                arg.UninstallString);
    }

    [SupportedOSPlatform("windows")]
    protected override IEnumerable<DriverPackage> GetObjects(ProgramState state)
    {
        return Enumerator.GetDriverPackages();
    }

    protected override ImmutableArray<DriverPackageToUninstall> GetObjectsToUninstall(ProgramState state)
    {
        var driverPackageConfig = state.ConfigurationManager[DRIVER_PACKAGE_CONFIG];
        return JsonSerializer.Deserialize(driverPackageConfig, _serializerContext.ImmutableArrayDriverPackageToUninstall);
    }

    protected override void UninstallObject(ProgramState state, DriverPackage dp, DriverPackageToUninstall dpu)
    {
        try
        {
            switch (dpu.UninstallMethod)
            {
                case DriverPackageToUninstall.Normal:
                    run(Normal);
                    break;
                case DriverPackageToUninstall.Deferred:
                    run(Deferred);
                    break;
                case DriverPackageToUninstall.RegistryOnly:
                    RegistryOnly(dp);
                    break;
                default:
                    throw new NotSupportedException($"Uninstall method '{dpu.UninstallMethod}' is not supported");
            }
        }
        catch (AggregateException ex)
        {
            if (ex.InnerExceptions.Count == 1)
            {
                ExceptionDispatchInfo.Capture(ex.InnerExceptions[0]).Throw();
            }
        }

        void run(Func<ProgramState, DriverPackage, DriverPackageToUninstall, CancellationToken, Task> action)
        {
            var cts = new CancellationTokenSource();

            if (state.Interactive)
            {
                var tasks = new Task[]
                {
                    action(state, dp, dpu, cts.Token),
                    waitForUser()
                };

                ConsoleUtility.TemporaryPrint(() =>
                {
                    var i = Task.WaitAny(tasks);
                    cts.Cancel();
                    if (tasks[i].IsFaulted)
                    {
                        throw tasks[i].Exception!;
                    }
                });
            }
            else
            {
                action(state, dp, dpu, cts.Token).Wait();
            }

            Task waitForUser()
            {
                return Task.Run(async () =>
                {
                    if (state.Interactive)
                    {
                        Console.Write("Complete the uninstall process. If this message is not gone after uninstall is complete, then press any key to continue...");
                        await ConsoleUtility.ReadKeyAsync(cts.Token);
                    }
                }, cts.Token);
            }
        }
    }

    private Task Normal(ProgramState state, DriverPackage dp, DriverPackageToUninstall dpu, CancellationToken token)
    {
        return Task.Run(async () =>
        {
            var process = StartProcess(dp.UninstallString!);
            await WaitForAllToExitAsync(process, token);
        }, token);

        static async Task WaitForAllToExitAsync(Process process, CancellationToken ct)
        {
            ManagementObjectSearcher searcher = new(
                "SELECT * " +
                "FROM Win32_Process " +
                "WHERE ParentProcessId=" + process.Id);
            ManagementObjectCollection collection = searcher.Get();
            if (collection.Count > 0)
            {
                foreach (var item in collection)
                {
                    uint childProcessId = (uint)item["ProcessId"];
                    if ((int)childProcessId != Environment.ProcessId)
                    {
                        Process childProcess = Process.GetProcessById((int)childProcessId);
                        await WaitForAllToExitAsync(childProcess, ct);
                    }
                }
            }

            await process.WaitForExitAsync(ct);
        }
    }

    private static Task Deferred(ProgramState state, DriverPackage dp, DriverPackageToUninstall dpu, CancellationToken token)
    {
        return Task.Run(async () =>
        {
            await StartProcess(dp.UninstallString!)!.WaitForExitAsync(token);
            await Task.Delay(TimeSpan.FromSeconds(0.5), token);
            await waitForDelegation(dp, dpu, token);
        }, token);

        static async Task waitForDelegation(DriverPackage dp, DriverPackageToUninstall dpu, CancellationToken token)
        {
            Process? delegation = null;
            dp.UninstallString!.ExtractToArgs(out var command, out _);
            var targetDir = Path.GetDirectoryName(command)!;

            foreach (var process in Process.GetProcesses())
            {
                var commandLine = GetCommandLine(process);
                if (commandLine is null) continue;

                if (commandLine.Contains(targetDir))
                {
                    delegation = process;
                    break;
                }
            }

            if (delegation is null)
                return;

            await delegation.WaitForExitAsync(token);
        }
    }

    private static void RegistryOnly(DriverPackage dp)
    {
        var baseKeyName = dp.X86
            ? "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall"
            : "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall";

        var uninstallListKey = Registry.LocalMachine.OpenSubKey(baseKeyName, writable: true)!;
        uninstallListKey.DeleteSubKeyTree(Path.GetFileName(dp.KeyName));
    }

    private static string? GetCommandLine(Process process)
    {
        using var searcher = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id);
        using var objects = searcher.Get();

        return objects.Cast<ManagementBaseObject>().SingleOrDefault()?["CommandLine"]?.ToString();
    }

    private static Process StartProcess(string str)
    {
        var processStartInfo = str.ToProcessStartInfo();
        try
        {
            return Process.Start(processStartInfo)!;
        }
        catch
        {
            try
            {
                processStartInfo = str.ToProcessStartInfo(workaroundMissingQuote: true);
                return Process.Start(processStartInfo)!;
            }
            catch (Win32Exception e) when (e.NativeErrorCode == Win32Error.ERROR_FILE_NOT_FOUND)
            {
                throw new AlreadyUninstalledException();
            }
        }
    }
}

[JsonSerializable(typeof(ImmutableArray<DriverPackage>))]
[JsonSerializable(typeof(ImmutableArray<DriverPackageToUninstall>))]
internal partial class DriverPackageSerializerContext : JsonSerializerContext
{
}