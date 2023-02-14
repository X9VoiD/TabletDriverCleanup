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

    public static void TemporaryPrint(Action action)
    {
        (int Left, int Top) = Console.GetCursorPosition();
        action();
        Console.SetCursorPosition(Left, Top);
        ClearLine();
    }

    public static async Task<ConsoleKeyInfo?> ReadKeyAsync(CancellationToken ct = default)
    {
        while (!Console.KeyAvailable)
        {
            if (ct.IsCancellationRequested)
                throw new OperationCanceledException();
            await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
        }

        return Console.ReadKey();
    }
}

public enum PromptResult
{
    Yes,
    No,
    Cancel
}