namespace TabletDriverCleanup
{
    public interface ICleanupModule
    {
        string Name { get; }
        string CliName { get; }
        string DisablementDescription { get; }
        bool Enabled { get; set; }
        bool SupportsDump { get; }

        void Run(ProgramState state);
        void Dump(ProgramState state);
    }
}