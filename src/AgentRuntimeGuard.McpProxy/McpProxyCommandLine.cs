namespace AgentRuntimeGuard.McpProxy;

internal sealed record McpProxyCommandLineOptions(
    string PolicyPath,
    string Command,
    IReadOnlyList<string> CommandArguments,
    string? ReceiptDatabasePath,
    int MaxReceipts,
    string? SessionId);

internal static class McpProxyCommandLine
{
    public static bool TryParse(
        string[] args,
        out McpProxyCommandLineOptions? options,
        out string? error)
    {
        options = null;
        error = null;

        var separatorIndex = Array.IndexOf(args, "--");
        if (separatorIndex < 0)
        {
            error = "Missing '--' before the upstream MCP server command.";
            return false;
        }

        string? policyPath = null;
        string? receiptDatabasePath = null;
        string? sessionId = null;
        var maxReceipts = 10_000;
        var retentionWasSpecified = false;

        for (var index = 0; index < separatorIndex; index++)
        {
            var argument = args[index];

            switch (argument)
            {
                case "--policy":
                    if (!TryReadValue(args, ref index, separatorIndex, out policyPath))
                    {
                        error = "--policy requires a file path.";
                        return false;
                    }

                    break;

                case "--receipts":
                    if (!TryReadValue(
                            args,
                            ref index,
                            separatorIndex,
                            out receiptDatabasePath))
                    {
                        error = "--receipts requires a SQLite database path.";
                        return false;
                    }

                    break;

                case "--max-receipts":
                    if (!TryReadValue(args, ref index, separatorIndex, out var maxText)
                        || !int.TryParse(maxText, out maxReceipts)
                        || maxReceipts < 1)
                    {
                        error = "--max-receipts requires a positive integer.";
                        return false;
                    }

                    retentionWasSpecified = true;
                    break;

                case "--session-id":
                    if (!TryReadValue(args, ref index, separatorIndex, out sessionId)
                        || string.IsNullOrWhiteSpace(sessionId))
                    {
                        error = "--session-id requires a non-empty value.";
                        return false;
                    }

                    if (sessionId.Length > 128)
                    {
                        error = "--session-id must be 128 characters or fewer.";
                        return false;
                    }

                    break;

                default:
                    error = $"Unknown option '{argument}'.";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(policyPath))
        {
            error = "--policy is required.";
            return false;
        }

        if (retentionWasSpecified
            && string.IsNullOrWhiteSpace(receiptDatabasePath))
        {
            error = "--max-receipts requires --receipts.";
            return false;
        }

        if (separatorIndex + 1 >= args.Length)
        {
            error = "An upstream MCP server command is required after '--'.";
            return false;
        }

        options = new McpProxyCommandLineOptions(
            policyPath,
            args[separatorIndex + 1],
            args[(separatorIndex + 2)..],
            receiptDatabasePath,
            maxReceipts,
            sessionId);

        return true;
    }

    public static string Usage =>
        "Usage: AgentRuntimeGuard.McpProxy --policy <policy.json> " +
        "[--receipts <receipts.db>] [--max-receipts <count>] " +
        "[--session-id <id>] -- <command> [arguments...]";

    private static bool TryReadValue(
        string[] args,
        ref int index,
        int separatorIndex,
        out string? value)
    {
        value = null;

        if (++index >= separatorIndex)
        {
            return false;
        }

        value = args[index];
        return !string.IsNullOrWhiteSpace(value);
    }
}
