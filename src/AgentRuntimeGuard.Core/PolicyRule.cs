using AgentRuntimeGuard.Abstractions;

namespace AgentRuntimeGuard.Core;

public sealed class PolicyRule
{
    public string Id { get; set; } = string.Empty;

    public int Priority { get; set; }

    public PolicyEffect Effect { get; set; }

    public string ToolPattern { get; set; } = "*";

    public string OperationPattern { get; set; } = "*";

    public string ResourcePattern { get; set; } = "*";

    public string Reason { get; set; } = string.Empty;
}
