namespace TabletDriverCleanup;

public static class ConsoleUtility
{
    public static PromptResult PromptYesNo(string message)
    {
        Console.Write($"{message} [Y/n/q] ");

        var key = Console.ReadKey();
        Console.WriteLine();

        return key.Key switch
        {
            ConsoleKey.Y or ConsoleKey.Enter => PromptResult.Yes,
            ConsoleKey.Q => PromptResult.Cancel,
            _ => PromptResult.No,
        };
    }
}

public enum PromptResult
{
    Yes,
    No,
    Cancel
}