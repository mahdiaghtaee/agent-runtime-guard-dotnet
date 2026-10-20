# Agent Runtime Guard for .NET

[![CI](https://github.com/mahdiaghtaee/agent-runtime-guard-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/mahdiaghtaee/agent-runtime-guard-dotnet/actions/workflows/ci.yml)

A local-first .NET guard for evaluating AI-agent and MCP tool calls **before** they reach the upstream tool.

~~~text
MCP host
   |
   | tools/call
   v
Agent Runtime Guard
   |
   +-- read_file    -> ALLOW -------------> upstream MCP server
   |
   +-- write_file   -> DENY --------------X
   |
   +-- unknown_tool -> REQUIRE_APPROVAL ---X
~~~

A denied call is not merely hidden from the client: the proxy test suite verifies that it never reaches upstream input.

> **Status:** early development. Deterministic policy enforcement, stdio MCP interception, bounded local receipts, and an opt-in loopback human-approval path are implemented and covered by CI. This is not a production security product.

## What it does

- evaluates explicit `Allow`, `RequireApproval`, and `Deny` decisions;
- intercepts MCP `tools/call` before forwarding;
- passes allowed and non-tool JSON-RPC traffic through unchanged;
- fails closed for denied, rejected, expired, broker-failed, or malformed tool-call traffic;
- can expose pending approvals on a bearer-protected loopback-only HTTP endpoint;
- can record opt-in SQLite decision receipts with bounded metadata;
- can query/export receipts per session as JSON Lines.

## What it deliberately does not do

- no model-based risk scoring;
- no hosted control plane;
- no dashboard;
- no process sandboxing or network isolation;
- no raw prompt or full MCP argument persistence;
- no production security or compliance claim.

Local human approval for `RequireApproval` is available as an explicit opt-in path.

## HTTP quick start

Requirements: .NET 10 SDK.

~~~bash
dotnet run --project src/AgentRuntimeGuard.Api/AgentRuntimeGuard.Api.csproj
~~~

Evaluate a policy request:

~~~bash
curl -X POST http://localhost:5078/v1/evaluate -H "Content-Type: application/json" -d '{"agentId":"local-coding-agent","sessionId":"session-001","toolName":"filesystem","operation":"read","resource":"src/Program.cs","tags":["workspace"]}'
~~~

## MCP stdio proxy

The proxy launches an upstream MCP server and keeps protocol traffic on stdin/stdout.

~~~text
AgentRuntimeGuard.McpProxy --policy <policy.json> [receipt options] -- <command> [arguments...]
~~~

Example with the official filesystem MCP server:

~~~bash
dotnet run --project src/AgentRuntimeGuard.McpProxy -- --policy examples/policies/filesystem-readonly.json -- npx -y @modelcontextprotocol/server-filesystem .
~~~

On Windows, launch npx through cmd:

~~~powershell
dotnet run --project src/AgentRuntimeGuard.McpProxy -- --policy examples/policies/filesystem-readonly.json -- cmd /c npx -y @modelcontextprotocol/server-filesystem .
~~~

The sample policy allows read/list-style tools, denies write/edit-style tools, and defaults everything else to RequireApproval. Without approval explicitly enabled, RequireApproval remains fail-closed.

### MCP policy mapping

For an MCP tools/call request:

~~~text
params.name -> ToolName
tools/call  -> Operation
mcp:stdio   -> Resource
~~~

The complete MCP arguments object is not copied into the policy request or local policy error.

Behavior:

- **Allow**: forward the original JSON line unchanged.
- **Deny**: stop locally and return a JSON-RPC error.
- **RequireApproval without approval enabled**: stop locally.
- **RequireApproval with local approval enabled**: wait for explicit approval; only approval forwards the original request.
- Other valid JSON-RPC messages: pass through unchanged.
- Malformed JSON/tool-call envelopes: fail closed.

The current policy-block response uses project-defined JSON-RPC server error code **-32001** and bounded decision metadata. Upstream stderr is relayed to proxy stderr so stdout stays protocol-only.

## Optional local human approval

Approval is disabled by default. Enable it with:

~~~bash
dotnet run --project src/AgentRuntimeGuard.McpProxy -- \
  --policy examples/policies/filesystem-readonly.json \
  --approval-port 5087 \
  --approval-timeout-seconds 120 \
  -- npx -y @modelcontextprotocol/server-filesystem .
~~~

The HTTP server binds only to `127.0.0.1`. If no token source is configured, the proxy generates a cryptographically random bearer token and writes it once to stderr.

For an explicit token, store it in an environment variable and pass only the variable name:

~~~bash
export ARG_APPROVAL_TOKEN="<at-least-32-character-secret>"
dotnet run --project src/AgentRuntimeGuard.McpProxy -- \
  --policy examples/policies/filesystem-readonly.json \
  --approval-port 5087 \
  --approval-token-env ARG_APPROVAL_TOKEN \
  -- npx -y @modelcontextprotocol/server-filesystem .
~~~

List pending approvals:

~~~bash
curl -H "Authorization: Bearer $ARG_APPROVAL_TOKEN" http://127.0.0.1:5087/v1/approvals
~~~

Approve or reject with:

~~~text
POST /v1/approvals/<decision-id>/approve
POST /v1/approvals/<decision-id>/reject
~~~

Pending approval responses contain only decision/rule/session/tool/operation/resource metadata plus creation and expiry timestamps. Raw prompts, full MCP arguments, tool results, and upstream stderr are not exposed.

Timeout, rejection, missing provider, invalid bearer token, and broker failure all remain fail-closed.

The bearer token protects the loopback HTTP endpoint from unauthenticated requests; it is not OS user isolation or a remote identity system.

## Optional decision receipts

Receipt persistence is disabled unless `--receipts` is supplied.

Example:

~~~bash
dotnet run --project src/AgentRuntimeGuard.McpProxy -- \
  --policy examples/policies/filesystem-readonly.json \
  --receipts .guard/receipts.db \
  --max-receipts 10000 \
  --session-id local-demo-001 \
  -- npx -y @modelcontextprotocol/server-filesystem .
~~~

When enabled, each policy decision is written before it is returned to the proxy. If SQLite persistence fails, the policy evaluation fails rather than silently executing without the requested receipt.

Stored fields are intentionally bounded:

~~~text
DecisionId
EvaluatedAtUtc
Effect
RuleId
AgentId
SessionId
ToolName
Operation
Resource
~~~

Not stored:

~~~text
Prompt
MCP arguments
Policy reason
Tags
Tool result
Upstream stderr
~~~

Receipt retention is a global maximum count. Older rows are removed after a new receipt is inserted.

### Query or export receipts

Query a session:

~~~bash
dotnet run --project src/AgentRuntimeGuard.McpProxy -- receipts \
  --database .guard/receipts.db \
  --session local-demo-001 \
  --limit 50
~~~

The command prints one JSON object per line, newest first. Redirect stdout to create a JSONL export:

~~~bash
dotnet run --project src/AgentRuntimeGuard.McpProxy -- receipts \
  --database .guard/receipts.db \
  --session local-demo-001 > receipts.jsonl
~~~

SQLite storage is local and is not encrypted by this project. File-system access to the database remains the operator's responsibility.

## Policy rules

See examples/policies/filesystem-readonly.json.

A rule is explicit and deterministic:

~~~json
{
  "id": "deny-filesystem-write",
  "priority": 200,
  "effect": "Deny",
  "toolPattern": "write*",
  "operationPattern": "tools/call",
  "resourcePattern": "mcp:stdio",
  "reason": "Write-oriented filesystem tools are blocked by this demo policy."
}
~~~

When several rules match:

1. higher priority wins;
2. at the same priority, Deny outranks RequireApproval, which outranks Allow;
3. rule ID is the deterministic final tie-breaker.

## Repository layout

~~~text
src/
  AgentRuntimeGuard.Abstractions/
  AgentRuntimeGuard.Core/
  AgentRuntimeGuard.Approvals/
  AgentRuntimeGuard.Api/
  AgentRuntimeGuard.Receipts/
  AgentRuntimeGuard.McpProxy/

tests/
  AgentRuntimeGuard.Tests/

examples/
  policies/

docs/
  adr/
  architecture.md
  roadmap.md
~~~

## Trust boundary

The guard only enforces actions that actually pass through it.

For stdio MCP, the host must launch the guard proxy instead of launching the upstream MCP server directly. If an agent, user, or host can bypass the proxy and start the upstream server through another path, this project does not prevent that bypass.

The approval endpoint is loopback-only and bearer-protected, but it does not provide OS-user isolation, remote identity, multi-user authorization, or tamper-evident approval audit.

The proxy is not an OS sandbox, credential broker, network firewall, or process-isolation mechanism.

See docs/adr/0001-policy-before-execution.md, docs/adr/0002-transparent-mcp-stdio-proxy.md, docs/adr/0003-bounded-decision-receipts.md, and docs/adr/0004-local-human-approval.md.

## Design principles

- keep enforcement deterministic before adding model-based decisions;
- keep MCP concerns outside the policy core;
- prefer explicit decisions over opaque risk scores;
- fail conservatively when a tool call cannot be safely classified;
- minimize copied/stored sensitive payloads;
- make persistence opt-in and explicit;
- add features only after a concrete integration justifies them;
- keep limitations next to capability claims.

## Roadmap

See docs/roadmap.md.

With local approval implemented, the next useful direction is bounded approval tracing/metrics or agent-specific setup helpers only if real usage justifies them.

## Security

This repository is experimental. Do not use it as the only control protecting production credentials, shells, databases, or infrastructure.

See SECURITY.md.

## License

MIT
