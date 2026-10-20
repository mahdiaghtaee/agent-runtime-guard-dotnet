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
   | tools/call -> IPolicyEvaluator
   |                |
   |                +-> optional RecordingPolicyEvaluator
   |                |      |
   |                |      v
   |                |   SQLite receipts
   |                |
   | Allow ----------+
   v
Upstream MCP server child process
~~~

Valid JSON-RPC messages other than tools/call pass through unchanged. The proxy does not own MCP initialization, discovery, capability negotiation, notifications, or extension semantics.

Malformed client JSON and malformed tools/call envelopes fail closed.

## Projects

### AgentRuntimeGuard.Abstractions

Transport-neutral contracts: ToolActionRequest, PolicyEffect, PolicyEvaluationResult, and IPolicyEvaluator.

### AgentRuntimeGuard.Core

Deterministic policy matching, priority ordering, wildcard matching, and conservative defaults.

### AgentRuntimeGuard.Approvals

Optional human approval components:

- `IApprovalProvider` keeps approval outside Core;
- `LocalApprovalBroker` manages bounded pending requests and timeout/reject/approve outcomes;
- `LocalApprovalServer` exposes a bearer-protected HTTP surface bound only to IPv4 loopback;
- generated tokens use cryptographic random bytes;
- pending metadata excludes raw prompts and MCP arguments.

### AgentRuntimeGuard.Api

A small HTTP integration boundary for direct policy evaluation.

### AgentRuntimeGuard.Receipts

Optional local decision persistence:

- DecisionReceipt is deliberately smaller than the policy request/result pair;
- RecordingPolicyEvaluator decorates any IPolicyEvaluator;
- SqliteDecisionReceiptStore owns schema, retention, and session queries;
- Microsoft.Data.Sqlite is contained in this project and does not leak into Core.

### AgentRuntimeGuard.McpProxy

The stdio adapter:

- starts upstream without a shell;
- sends command arguments through ProcessStartInfo.ArgumentList;
- inspects only enough JSON-RPC structure to identify tools/call;
- maps tool name into the existing policy contract;
- optionally decorates the evaluator with receipt persistence;
- optionally waits for local human approval after `RequireApproval`;
- forwards the original request after `Allow` or explicit approval;
- blocks `Deny`, rejection, expiry, missing-provider, and broker-failure paths;
- relays upstream stdout responses/errors unchanged;
- sends upstream diagnostics only to stderr.

## Decision receipt schema

SQLite table: decision_receipts

Stored columns:

- sequence;
- decision_id;
- evaluated_at_utc;
- effect;
- rule_id;
- agent_id;
- session_id;
- tool_name;
- operation;
- resource.

The schema deliberately excludes prompt, arguments, policy reason, tags, tool result, and upstream stderr.

String metadata is clipped to documented maximum lengths before persistence. Receipt query is session-scoped, newest first, and limited to at most 1000 rows per call.

Retention is deterministic: after each insert, rows older than the newest configured maximum are removed in the same transaction.

## Persistence failure behavior

Receipts are disabled by default.

When enabled, RecordingPolicyEvaluator writes the receipt before returning the decision. A store failure propagates to the integration instead of returning an unrecorded Allow/Deny/RequireApproval decision.

This is intentionally conservative: opting into receipts means the operator has asked the guard not to execute policy decisions without them.

## Data minimization

For stdio MCP the adapter maps:

- params.name to ToolName;
- tools/call to Operation;
- mcp:stdio to Resource;
- a local proxy-instance ID to the correlation/session field.

The MCP arguments object is not copied into policy results or decision receipts.

Argument-aware policy is intentionally deferred until there is an explicit redaction/retention design.

## Enforcement limitation

The proxy creates an enforcement point only when the host is configured to use it.

It does not stop a sufficiently privileged actor from launching the upstream server directly, and it does not provide process sandboxing, network isolation, credential brokering, database encryption, or authenticated receipt access.

## Approval boundary

For a `RequireApproval` decision, the MCP adapter sends only bounded metadata to `IApprovalProvider`: decision ID, effect, rule ID, session ID, tool name, operation, and resource. The local broker adds creation and expiry timestamps.

The first server binds directly to `IPAddress.Loopback`; approval routes require a bearer token. Explicit tokens are loaded from an environment variable rather than passed as a raw command-line secret.

The complete MCP `arguments` object is never copied into the approval request or pending-approval HTTP response.

Approval remains fail-closed: timeout, rejection, cancellation, missing provider, and broker errors never turn `RequireApproval` into `Allow`.
