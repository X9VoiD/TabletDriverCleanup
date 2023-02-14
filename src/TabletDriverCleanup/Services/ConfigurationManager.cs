using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace TabletDriverCleanup;

public class ConfigurationManager
{
    private const string CONFIG_BASE_URL = "https://raw.githubusercontent.com/X9VoiD/TabletDriverCleanup";
    private const string REF = "v3.x";
    private readonly ProgramState _state;

    public ConfigurationManager(ProgramState state)
    {
        _state = state;
    }

    public string this[string configurationName]
        => TryGetConfiguration(configurationName, out string? configuration)
            ? configuration
            : throw new ArgumentException($"Configuration '{configurationName}' not found", nameof(configurationName));

    public bool TryGetConfiguration(string configurationName, [NotNullWhen(true)] out string? configuration)
    {
        if (TryGetConfigurationOffline(configurationName, out configuration)
            || TryGetConfigurationOnline(configurationName, out configuration)
            || TryGetConfigurationInAssembly(configurationName, out configuration))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private bool TryGetConfigurationOffline(string configurationName, [NotNullWhen(true)] out string? configuration)
    {
        if (_state.NoCache)
        {
            configuration = null;
            return false;
        }

        string targetPath = Path.Join(_state.CurrentPath, "config", configurationName);
        if (!File.Exists(targetPath))
        {
            configuration = null;
            return false;
        }

        try
        {
            configuration = File.ReadAllText(targetPath);
            return true;
        }
        catch
        {
            configuration = null;
            return false;
        }
    }

    private bool TryGetConfigurationOnline(string configurationName, [NotNullWhen(true)] out string? configuration)
    {
        if (_state.NoUpdate)
        {
            configuration = null;
            return false;
        }

        string targetDir = _state.NoCache
            ? Path.Join(Path.GetTempPath(), Path.GetRandomFileName(), "config")
            : Path.Join(_state.CurrentPath, "config");

        string targetPath = Path.Join(targetDir, configurationName);
        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);

        try
        {
            Downloader.Download($"{CONFIG_BASE_URL}/{REF}/config/{configurationName}", targetPath);
            configuration = File.ReadAllText(targetPath);
            return true;
        }
        catch
        {
            configuration = null;
            return false;
        }
    }

    private static bool TryGetConfigurationInAssembly(string configurationName, [NotNullWhen(true)] out string? configuration)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string resourceName = $"TabletDriverCleanup.{configurationName}";

        try
        {
            using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
            using StreamReader reader = new(stream);
            configuration = reader.ReadToEnd();
            return true;
        }
        catch
        {
            configuration = null;
            return false;
        }
    }
}