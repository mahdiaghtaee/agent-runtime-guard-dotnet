namespace AgentRuntimeGuard.McpProxy;

internal sealed record McpProxyCommandLineOptions(
    string PolicyPath,
    string Command,
    IReadOnlyList<string> CommandArguments,
    string? ReceiptDatabasePath,
    int MaxReceipts,
    string? SessionId,
    int? ApprovalPort,
    int ApprovalTimeoutSeconds,
    string? ApprovalTokenEnvironmentVariable);

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
        string? approvalTokenEnvironmentVariable = null;
        int? approvalPort = null;
        var maxReceipts = 10_000;
        var approvalTimeoutSeconds = 120;
        var retentionWasSpecified = false;
        var approvalTimeoutWasSpecified = false;
        var approvalTokenWasSpecified = false;

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

                case "--approval-port":
                    if (!TryReadValue(args, ref index, separatorIndex, out var portText)
                        || !int.TryParse(portText, out var parsedPort)
                        || parsedPort is < 1 or > 65535)
                    {
                        error = "--approval-port must be between 1 and 65535.";
                        return false;
                    }

                    approvalPort = parsedPort;
                    break;

                case "--approval-timeout-seconds":
                    if (!TryReadValue(args, ref index, separatorIndex, out var timeoutText)
                        || !int.TryParse(timeoutText, out approvalTimeoutSeconds)
                        || approvalTimeoutSeconds is < 1 or > 3600)
                    {
                        error = "--approval-timeout-seconds must be between 1 and 3600.";
                        return false;
                    }

                    approvalTimeoutWasSpecified = true;
                    break;

                case "--approval-token-env":
                    if (!TryReadValue(
                            args,
                            ref index,
                            separatorIndex,
                            out approvalTokenEnvironmentVariable)
                        || approvalTokenEnvironmentVariable!.Contains('='))
                    {
                        error = "--approval-token-env requires a valid environment variable name.";
                        return false;
                    }

                    approvalTokenWasSpecified = true;
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

        if ((approvalTimeoutWasSpecified || approvalTokenWasSpecified)
            && approvalPort is null)
        {
            error = "--approval-timeout-seconds and --approval-token-env require --approval-port.";
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
            sessionId,
            approvalPort,
            approvalTimeoutSeconds,
            approvalTokenEnvironmentVariable);

        return true;
    }

    public static string Usage =>
        "Usage: AgentRuntimeGuard.McpProxy --policy <policy.json> " +
        "[--receipts <receipts.db>] [--max-receipts <count>] " +
        "[--session-id <id>] " +
        "[--approval-port <port>] [--approval-timeout-seconds <1-3600>] " +
        "[--approval-token-env <name>] -- <command> [arguments...]";

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
