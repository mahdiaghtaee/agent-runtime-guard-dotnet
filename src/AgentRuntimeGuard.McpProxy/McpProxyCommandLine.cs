namespace AgentRuntimeGuard.McpProxy;

internal sealed record McpProxyCommandLineOptions(
    string PolicyPath,
    string Command,
    IReadOnlyList<string> CommandArguments);

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

        for (var index = 0; index < separatorIndex; index++)
        {
            if (!string.Equals(args[index], "--policy", StringComparison.Ordinal))
            {
                error = $"Unknown option '{args[index]}'.";
                return false;
            }

            if (++index >= separatorIndex)
            {
                error = "--policy requires a file path.";
                return false;
            }

            policyPath = args[index];
        }

        if (string.IsNullOrWhiteSpace(policyPath))
        {
            error = "--policy is required.";
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
            args[(separatorIndex + 2)..]);

        return true;
    }

    public static string Usage =>
        "Usage: AgentRuntimeGuard.McpProxy --policy <policy.json> -- <command> [arguments...]";
}
