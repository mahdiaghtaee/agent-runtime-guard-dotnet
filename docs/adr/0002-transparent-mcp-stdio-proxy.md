# ADR 0002: Intercept MCP tool calls without owning protocol negotiation

- Status: Accepted
- Date: 2026-10-09

## Context

The project needs a concrete execution path where policy is evaluated before a tool side effect.

MCP stdio is a useful first boundary, but MCP protocol revisions have changed connection/session behavior. The guard should not become a second MCP implementation just to protect tool execution.

## Decision

The stdio proxy stays transport-thin:

1. launch the upstream process without a shell;
2. relay upstream stdout back to the host;
3. inspect client-to-server JSON-RPC lines;
4. pass valid messages other than tools/call through unchanged;
5. map tools/call params.name into the existing policy contract;
6. forward the original line only for Allow;
7. stop Deny and RequireApproval before upstream execution.

The proxy does not interpret initialization, discovery, capability negotiation, notifications, task extensions, or other protocol-version behavior.

Malformed JSON and malformed tools/call requests fail closed locally.

## Policy mapping

- ToolName = params.name
- Operation = tools/call
- Resource = mcp:stdio
- SessionId = a local proxy-instance correlation identifier

That local identifier is not an MCP protocol session and is not authenticated identity.

The adapter does not copy the MCP arguments object into the policy request.

## Local block response

A blocked tool call uses project-defined JSON-RPC server error code -32001.

The response contains bounded decision metadata: effect, decision ID, matched rule ID when present, and configured reason. It does not echo original tool arguments.

A future standardized authorization-denial mechanism may replace this local shape once such behavior is stable for the protocol revisions the project supports.

## Consequences

Positive:

- Core remains MCP-independent;
- allowed JSON lines reach upstream unchanged;
- denied calls can be proven absent from upstream input;
- upstream result/error semantics stay owned by upstream;
- protocol negotiation remains outside the guard.

Tradeoffs:

- the proxy is not a full MCP validator;
- RequireApproval must block until a separate approval adapter exists;
- argument-level policy is not available yet;
- bypass remains possible when an actor can launch upstream directly.

## Follow-up

The next milestone is bounded local decision receipts. Approval UX and argument-aware policies remain separate work.
