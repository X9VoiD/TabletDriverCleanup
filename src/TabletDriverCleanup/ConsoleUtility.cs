namespace TabletDriverCleanup;

public static class ConsoleUtility
{
    public static PromptResult PromptYesNo(string message)
    {
        (int Left, int Top) = Console.GetCursorPosition();
        Console.Write($"{message} [Y/n/q] ");

        var key = Console.ReadKey();
        Console.SetCursorPosition(Left, Top);
        ClearLine();

        return key.Key switch
        {
            ConsoleKey.Y or ConsoleKey.Enter => PromptResult.Yes,
            ConsoleKey.Q => PromptResult.Cancel,
            _ => PromptResult.No,
        };
    }

    public static void ClearLine()
    {
        Console.Write(new string(' ', Console.BufferWidth - 1) + "\r");
    }
}

public enum PromptResult
{
    Yes,
    No,
    Cancel
}