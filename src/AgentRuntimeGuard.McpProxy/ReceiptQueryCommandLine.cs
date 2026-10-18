namespace AgentRuntimeGuard.McpProxy;

internal sealed record ReceiptQueryCommandLineOptions(
    string DatabasePath,
    string SessionId,
    int Limit);

internal static class ReceiptQueryCommandLine
{
    public static bool TryParse(
        string[] args,
        out ReceiptQueryCommandLineOptions? options,
        out string? error)
    {
        options = null;
        error = null;

        string? databasePath = null;
        string? sessionId = null;
        var limit = 100;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--database":
                    if (!TryReadValue(args, ref index, out databasePath))
                    {
                        error = "--database requires a SQLite database path.";
                        return false;
                    }

                    break;

                case "--session":
                    if (!TryReadValue(args, ref index, out sessionId))
                    {
                        error = "--session requires a session id.";
                        return false;
                    }

                    break;

                case "--limit":
                    if (!TryReadValue(args, ref index, out var limitText)
                        || !int.TryParse(limitText, out limit)
                        || limit is < 1 or > 1_000)
                    {
                        error = "--limit must be between 1 and 1000.";
                        return false;
                    }

                    break;

                default:
                    error = $"Unknown receipts option '{args[index]}'.";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(databasePath))
        {
            error = "--database is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            error = "--session is required.";
            return false;
        }

        options = new ReceiptQueryCommandLineOptions(
            databasePath,
            sessionId,
            limit);

        return true;
    }

    public static string Usage =>
        "Usage: AgentRuntimeGuard.McpProxy receipts " +
        "--database <receipts.db> --session <id> [--limit <1-1000>]";

    private static bool TryReadValue(
        string[] args,
        ref int index,
        out string? value)
    {
        value = null;

        if (++index >= args.Length)
        {
            return false;
        }

        value = args[index];
        return !string.IsNullOrWhiteSpace(value);
    }
}
