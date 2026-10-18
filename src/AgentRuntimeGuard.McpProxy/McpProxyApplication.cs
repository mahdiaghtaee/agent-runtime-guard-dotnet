using System.Text.Json;
using System.Text.Json.Serialization;
using AgentRuntimeGuard.Abstractions;
using AgentRuntimeGuard.Core;
using AgentRuntimeGuard.Receipts;

namespace AgentRuntimeGuard.McpProxy;

internal static class McpProxyApplication
{
    private static readonly JsonSerializerOptions s_receiptJsonOptions =
        new()
        {
            Converters = { new JsonStringEnumConverter() }
        };

    public static async Task<int> RunAsync(
        string[] args,
        TextReader input,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (args.Length > 0
            && string.Equals(
                args[0],
                "receipts",
                StringComparison.Ordinal))
        {
            return await RunReceiptQueryAsync(
                args[1..],
                output,
                error,
                cancellationToken);
        }

        if (!McpProxyCommandLine.TryParse(
                args,
                out var options,
                out var parseError))
        {
            await error.WriteLineAsync(parseError);
            await error.WriteLineAsync(McpProxyCommandLine.Usage);
            await error.WriteLineAsync(ReceiptQueryCommandLine.Usage);
            return 2;
        }

        try
        {
            var ruleSet = await PolicyFileLoader.LoadAsync(
                options!.PolicyPath,
                cancellationToken);

            IPolicyEvaluator evaluator = new PolicyEvaluator(ruleSet);
            var sessionId = options.SessionId
                ?? Guid.NewGuid().ToString("N");

            if (!string.IsNullOrWhiteSpace(options.ReceiptDatabasePath))
            {
                var store = new SqliteDecisionReceiptStore(
                    options.ReceiptDatabasePath,
                    options.MaxReceipts);

                await store.InitializeAsync(cancellationToken);
                evaluator = new RecordingPolicyEvaluator(
                    evaluator,
                    store);

                await error.WriteLineAsync(
                    $"[guard] receipts enabled; session={sessionId}");
            }

            var gate = new McpToolCallPolicyGate(
                evaluator,
                sessionId);

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

    private static async Task<int> RunReceiptQueryAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!ReceiptQueryCommandLine.TryParse(
                args,
                out var options,
                out var parseError))
        {
            await error.WriteLineAsync(parseError);
            await error.WriteLineAsync(ReceiptQueryCommandLine.Usage);
            return 2;
        }

        try
        {
            if (!File.Exists(options!.DatabasePath))
            {
                await error.WriteLineAsync(
                    $"Receipt database not found: {options.DatabasePath}");
                return 1;
            }

            var store = new SqliteDecisionReceiptStore(
                options.DatabasePath);

            await store.InitializeAsync(cancellationToken);

            var receipts = await store.QueryBySessionAsync(
                options.SessionId,
                options.Limit,
                cancellationToken);

            foreach (var receipt in receipts)
            {
                await output.WriteLineAsync(
                    JsonSerializer.Serialize(
                        receipt,
                        s_receiptJsonOptions));
            }

            return 0;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return 130;
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync(
                $"Receipt query failed: {exception.Message}");
            return 1;
        }
    }
}
