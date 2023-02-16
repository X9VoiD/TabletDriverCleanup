namespace TabletDriverCleanup;

public static class ConsoleUtility
{
    public static PromptResult PromptYesNo(string message)
    {
        ConsoleKeyInfo key = default;
        TemporaryPrint(() =>
        {
            Console.Write($"{message} [Y/n/q] ");
            key = Console.ReadKey();
        });

        return key.Key switch
        {
            ConsoleKey.Y or ConsoleKey.Enter => PromptResult.Yes,
            ConsoleKey.Q => PromptResult.Cancel,
            _ => PromptResult.No,
        };
    }

    public static void TemporaryPrint(Action action)
    {
        (int origLeft, int origTop) = Console.GetCursorPosition();
        action();
        (int newLeft, int newTop) = Console.GetCursorPosition();
        Console.SetCursorPosition(origLeft, origTop);

        var width = Console.BufferWidth;

        var top = width - origLeft;
        var inbetween = width * (newTop - origTop - 1);
        var bottom = width - (newLeft - 1);

        var totalCharacters = top + inbetween + bottom;
        Console.Write(new string(' ', totalCharacters));

        Console.SetCursorPosition(origLeft, origTop);
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