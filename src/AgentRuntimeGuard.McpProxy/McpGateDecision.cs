namespace AgentRuntimeGuard.McpProxy;

internal sealed record McpGateDecision(
    bool Forward,
    string? ClientResponse)
{
    public static McpGateDecision ForwardUnchanged { get; } = new(true, null);
}
