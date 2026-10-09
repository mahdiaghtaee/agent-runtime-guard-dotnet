# Architecture

## Policy boundary

The core turns a proposed tool action into a deterministic decision and never executes the action itself.

~~~text
Agent / integration
        |
        v
PolicyEvaluator
   |       |       |
 Allow  Approval  Deny
~~~

Keeping evaluation separate from execution makes the behavior testable without an agent SDK or external model.

## MCP stdio enforcement path

The MCP adapter adds one concrete pre-execution path while keeping MCP protocol concerns out of Core.

~~~text
MCP host
   |
   | stdin JSON-RPC
   v
AgentRuntimeGuard.McpProxy
   |
   | tools/call -> PolicyEvaluator
   |                |
   | Allow ----------+
   v
Upstream MCP server child process
   |
   v
result/error -> host
~~~

Valid JSON-RPC messages other than tools/call pass through unchanged. The proxy does not own MCP initialization, discovery, capability negotiation, notifications, or extension semantics.

Malformed client JSON and malformed tools/call envelopes fail closed.

## Projects

### AgentRuntimeGuard.Abstractions

Transport-neutral contracts: ToolActionRequest, PolicyEffect, PolicyEvaluationResult, and IPolicyEvaluator.

### AgentRuntimeGuard.Core

Deterministic policy matching, priority ordering, wildcard matching, and conservative defaults.

### AgentRuntimeGuard.Api

A small HTTP integration boundary for direct policy evaluation.

### AgentRuntimeGuard.McpProxy

The stdio adapter:

- starts upstream without a shell;
- sends command arguments through ProcessStartInfo.ArgumentList;
- inspects only enough JSON-RPC structure to identify tools/call;
- maps tool name into the existing policy contract;
- forwards allowed requests unchanged;
- blocks Deny and RequireApproval before upstream execution;
- relays upstream stdout responses/errors unchanged;
- sends upstream diagnostics only to stderr.

## Data minimization

For stdio MCP the adapter maps:

- params.name to ToolName;
- tools/call to Operation;
- mcp:stdio to Resource;
- a local proxy-instance ID to the correlation/session field.

The MCP arguments object is not copied into policy results or local block responses.

Argument-aware policy is intentionally deferred until there is an explicit redaction/retention design.

## Enforcement limitation

The proxy creates an enforcement point only when the host is configured to use it.

It does not stop a sufficiently privileged actor from launching the upstream server directly, and it does not provide process sandboxing, network isolation, or credential brokering.
