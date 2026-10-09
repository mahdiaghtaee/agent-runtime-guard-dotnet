using System.Text;
using System.Text.Json;
using AgentRuntimeGuard.Abstractions;

namespace AgentRuntimeGuard.McpProxy;

internal sealed class McpToolCallPolicyGate
{
    internal const int PolicyBlockedErrorCode = -32001;

    private readonly IPolicyEvaluator _evaluator;
    private readonly string _proxyInstanceId;

    public McpToolCallPolicyGate(
        IPolicyEvaluator evaluator,
        string proxyInstanceId)
    {
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _proxyInstanceId = string.IsNullOrWhiteSpace(proxyInstanceId)
            ? throw new ArgumentException(
                "A proxy instance id is required.",
                nameof(proxyInstanceId))
            : proxyInstanceId;
    }

    public async ValueTask<McpGateDecision> EvaluateAsync(
        string line,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(line);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(
                line,
                new JsonDocumentOptions { MaxDepth = 64 });
        }
        catch (JsonException)
        {
            return new McpGateDecision(
                false,
                CreateErrorResponse(
                    id: null,
                    code: -32700,
                    message: "Invalid JSON-RPC payload."));
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new McpGateDecision(
                    false,
                    CreateErrorResponse(
                        id: null,
                        code: -32600,
                        message: "Invalid JSON-RPC request."));
            }

            if (!root.TryGetProperty("method", out var methodElement))
            {
                return McpGateDecision.ForwardUnchanged;
            }

            if (methodElement.ValueKind != JsonValueKind.String)
            {
                return new McpGateDecision(
                    false,
                    CreateErrorResponse(
                        GetRequestId(root),
                        -32600,
                        "Invalid JSON-RPC method."));
            }

            var method = methodElement.GetString();
            if (!string.Equals(method, "tools/call", StringComparison.Ordinal))
            {
                return McpGateDecision.ForwardUnchanged;
            }

            var id = GetRequestId(root);

            if (!TryGetToolName(root, out var toolName))
            {
                return new McpGateDecision(
                    false,
                    CreateErrorResponse(
                        id,
                        -32602,
                        "Invalid params: tools/call requires params.name."));
            }

            var evaluation = await _evaluator.EvaluateAsync(
                new ToolActionRequest
                {
                    AgentId = "mcp-stdio-client",
                    SessionId = _proxyInstanceId,
                    ToolName = toolName,
                    Operation = "tools/call",
                    Resource = "mcp:stdio",
                    Tags = ["mcp", "stdio"]
                },
                cancellationToken);

            if (evaluation.Effect == PolicyEffect.Allow)
            {
                return McpGateDecision.ForwardUnchanged;
            }

            var message = evaluation.Effect == PolicyEffect.Deny
                ? "Tool call blocked by local policy."
                : "Tool call requires approval, but no approval adapter is configured.";

            return new McpGateDecision(
                false,
                CreatePolicyErrorResponse(id, message, evaluation));
        }
    }

    private static bool TryGetToolName(
        JsonElement root,
        out string toolName)
    {
        toolName = string.Empty;

        if (!root.TryGetProperty("params", out var parameters)
            || parameters.ValueKind != JsonValueKind.Object
            || !parameters.TryGetProperty("name", out var name)
            || name.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var value = name.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        toolName = value;
        return true;
    }

    private static JsonElement? GetRequestId(JsonElement root)
    {
        if (!root.TryGetProperty("id", out var id))
        {
            return null;
        }

        return id.ValueKind is JsonValueKind.String
            or JsonValueKind.Number
            or JsonValueKind.Null
            ? id.Clone()
            : null;
    }

    private static string CreatePolicyErrorResponse(
        JsonElement? id,
        string message,
        PolicyEvaluationResult evaluation)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", "2.0");
            WriteId(writer, id);

            writer.WritePropertyName("error");
            writer.WriteStartObject();
            writer.WriteNumber("code", PolicyBlockedErrorCode);
            writer.WriteString("message", message);

            writer.WritePropertyName("data");
            writer.WriteStartObject();
            writer.WriteString("effect", evaluation.Effect.ToString());
            writer.WriteString("decisionId", evaluation.DecisionId);
            if (!string.IsNullOrWhiteSpace(evaluation.RuleId))
            {
                writer.WriteString("ruleId", evaluation.RuleId);
            }

            writer.WriteString("reason", evaluation.Reason);
            writer.WriteEndObject();
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string CreateErrorResponse(
        JsonElement? id,
        int code,
        string message)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", "2.0");
            WriteId(writer, id);

            writer.WritePropertyName("error");
            writer.WriteStartObject();
            writer.WriteNumber("code", code);
            writer.WriteString("message", message);
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteId(
        Utf8JsonWriter writer,
        JsonElement? id)
    {
        writer.WritePropertyName("id");

        if (id is null)
        {
            writer.WriteNullValue();
            return;
        }

        id.Value.WriteTo(writer);
    }
}
