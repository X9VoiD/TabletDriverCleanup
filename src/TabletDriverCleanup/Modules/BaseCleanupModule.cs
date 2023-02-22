using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using TabletDriverCleanup.Services;

namespace TabletDriverCleanup.Modules;

public abstract class BaseCleanupModule<TObject, TObjectToUninstall> : ICleanupModule
    where TObject : class
    where TObjectToUninstall : class, IObjectToUninstall
{
    public abstract string Name { get; }
    public abstract string CliName { get; }
    public abstract string DisablementDescription { get; }
    public bool Enabled { get; set; } = true;

    public abstract bool SupportsDump { get; }

    protected abstract string Noun { get; }

    private readonly RegexCache _regexCache = new(RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);

    public void Run(ProgramState state)
    {
        var objects = GetObjects(state);
        var objectsToUninstall = GetObjectsToUninstall(state);

        var found = false;
        foreach (var @object in objects)
        {
            if (!ShouldUninstall(objectsToUninstall, @object, out var objectToUninstall))
                continue;

            found = true;
            if (state.Interactive && !state.DryRun)
            {
                var promptResult = ConsoleUtility.PromptYesNo($"Uninstall '{objectToUninstall}'?");
                if (promptResult == PromptResult.No)
                {
                    Console.WriteLine($"Skipping '{objectToUninstall}'...");
                    continue;
                }
                else if (promptResult == PromptResult.Cancel)
                {
                    Environment.Exit(0);
                }
            }
            Console.WriteLine($"Uninstalling '{objectToUninstall}'...");

            if (!state.DryRun)
            {
                try
                {
                    UninstallObject(state, @object, objectToUninstall);
                }
                catch (AlreadyUninstalledException)
                {
                    Console.WriteLine($"  '{objectToUninstall}' is already uninstalled by a previous uninstaller.");
                }
            }
        }

        if (!found)
            Console.WriteLine($"No {Noun} to uninstall is found.");
    }

    protected FileStream GetDumpFileStream(ProgramState state, string fileName)
    {
        var dumpDirectory = Path.Combine(state.CurrentPath, "dumps");
        if (!Directory.Exists(dumpDirectory))
            Directory.CreateDirectory(dumpDirectory);

        return File.Open(Path.Combine(dumpDirectory, fileName), FileMode.Create, FileAccess.Write);
    }

    private bool ShouldUninstall(ImmutableArray<TObjectToUninstall> objectsToUninstall, TObject @object, [NotNullWhen(true)] out TObjectToUninstall? objectToUninstall)
    {
        objectToUninstall = null;

        foreach (var objectToUninstallCandidate in objectsToUninstall)
        {
            if (objectToUninstallCandidate.Matches(_regexCache, @object))
            {
                objectToUninstall = objectToUninstallCandidate;
                return true;
            }
        }

        return false;
    }

    public abstract void Dump(ProgramState state);

    protected abstract IEnumerable<TObject> GetObjects(ProgramState state);
    protected abstract ImmutableArray<TObjectToUninstall> GetObjectsToUninstall(ProgramState state);
    protected abstract void UninstallObject(ProgramState state, TObject @object, TObjectToUninstall objectToUninstall);

    protected class AlreadyUninstalledException : Exception
    {
    }
}