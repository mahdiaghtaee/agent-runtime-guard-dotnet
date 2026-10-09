using AgentRuntimeGuard.Core;

namespace AgentRuntimeGuard.McpProxy;

internal static class McpProxyApplication
{
    public static async Task<int> RunAsync(
        string[] args,
        TextReader input,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!McpProxyCommandLine.TryParse(
                args,
                out var options,
                out var parseError))
        {
            await error.WriteLineAsync(parseError);
            await error.WriteLineAsync(McpProxyCommandLine.Usage);
            return 2;
        }

        try
        {
            var ruleSet = await PolicyFileLoader.LoadAsync(
                options!.PolicyPath,
                cancellationToken);

            var evaluator = new PolicyEvaluator(ruleSet);
            var gate = new McpToolCallPolicyGate(
                evaluator,
                Guid.NewGuid().ToString("N"));

            var session = new McpStdioProxySession(gate);
            var runner = new StdioProxyProcessRunner();

            return await runner.RunAsync(
                session,
                options.Command,
                options.CommandArguments,
                input,
                output,
                error,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return 130;
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync(
                $"Agent Runtime Guard proxy failed: {exception.Message}");
            return 1;
        }
    }
}
