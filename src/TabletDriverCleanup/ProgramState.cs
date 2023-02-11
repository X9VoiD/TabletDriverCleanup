namespace TabletDriverCleanup;

public class ProgramState
{
    public string CurrentPath { get; } = AppDomain.CurrentDomain.BaseDirectory;
    public IReadOnlyList<ICleanupModule> Modules { get; }
    public ConfigurationManager ConfigurationManager { get; }

    // CLI args
    public bool Interactive { get; set; } = true;
    public bool DryRun { get; set; }
    public bool Dump { get; set; }

    // Runtime state
    public bool RebootNeeded { get; set; }
    public Dictionary<string, object> Data { get; } = new();

    public ProgramState(ICleanupModule[] modules)
    {
        Modules = modules;
        ConfigurationManager = new ConfigurationManager(this);
    }
}