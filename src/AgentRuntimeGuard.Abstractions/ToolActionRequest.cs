namespace AgentRuntimeGuard.Abstractions;

public sealed record ToolActionRequest
{
    public string AgentId { get; init; } = string.Empty;

    public string SessionId { get; init; } = string.Empty;

    public string ToolName { get; init; } = string.Empty;

    public string Operation { get; init; } = string.Empty;

    public string Resource { get; init; } = string.Empty;

    public IReadOnlyCollection<string> Tags { get; init; } = Array.Empty<string>();
}
