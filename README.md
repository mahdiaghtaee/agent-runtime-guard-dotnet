# Agent Runtime Guard for .NET

[![CI](https://github.com/mahdiaghtaee/agent-runtime-guard-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/mahdiaghtaee/agent-runtime-guard-dotnet/actions/workflows/ci.yml)

A small .NET service for evaluating AI-agent tool actions before execution.

The project keeps one boundary explicit: an agent proposes a tool action, local deterministic policy returns **Allow**, **RequireApproval**, or **Deny**, and the integration decides whether execution may continue.

> **Status:** early development. The policy boundary is established; the stdio MCP enforcement path is being validated. This is not a production security product.

## Current scope

Implemented:

- provider-neutral policy contracts;
- deterministic rule precedence and wildcard matching;
- conservative configurable defaults;
- an ASP.NET Core evaluation endpoint;
- a stdio MCP child-process proxy;
- interception of MCP tools/call before upstream execution;
- unchanged forwarding of allowed and non-tool JSON-RPC traffic;
- local errors for Deny and currently-unhandled RequireApproval decisions;
- fail-closed handling for malformed client JSON;
- tests proving a denied tool call never reaches the upstream side of the proxy;
- CI and ADRs for the trust boundary.

Deliberately not implemented yet:

- approval UX/workflows;
- persistent decision receipts;
- replay;
- argument-level secret or command inspection;
- agent-specific adapters;
- multi-tenant policy administration;
- dashboards or hosted control-plane features.

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
AgentRuntimeGuard.McpProxy --policy <policy.json> -- <command> [arguments...]
~~~

Example with the official filesystem MCP server:

~~~bash
dotnet run --project src/AgentRuntimeGuard.McpProxy -- --policy examples/policies/filesystem-readonly.json -- npx -y @modelcontextprotocol/server-filesystem .
~~~

On Windows, launch npx through cmd:

~~~powershell
dotnet run --project src/AgentRuntimeGuard.McpProxy -- --policy examples/policies/filesystem-readonly.json -- cmd /c npx -y @modelcontextprotocol/server-filesystem .
~~~

The sample policy allows read/list-style tools, denies write/edit-style tools, and defaults everything else to RequireApproval. Since no approval adapter exists yet, RequireApproval stops the call.

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
- **RequireApproval**: stop locally until an approval adapter exists.
- Other valid JSON-RPC messages: pass through unchanged.
- Malformed JSON/tool-call envelopes: fail closed.

The current policy-block response uses project-defined JSON-RPC server error code **-32001** and bounded decision metadata. Upstream stderr is relayed to proxy stderr so stdout stays protocol-only.

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
  AgentRuntimeGuard.Api/
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

The proxy is not an OS sandbox, credential broker, network firewall, or process-isolation mechanism.

See docs/adr/0001-policy-before-execution.md and docs/adr/0002-transparent-mcp-stdio-proxy.md.

## Design principles

- keep enforcement deterministic before adding model-based decisions;
- keep MCP concerns outside the policy core;
- prefer explicit decisions over opaque risk scores;
- fail conservatively when a tool call cannot be safely classified;
- minimize copied/stored sensitive payloads;
- add features only after a concrete integration justifies them;
- keep limitations next to capability claims.

## Roadmap

See docs/roadmap.md.

After the MCP proxy, the next planned milestone is bounded local decision receipts, not a dashboard or hosted control plane.

## Security

This repository is experimental. Do not use it as the only control protecting production credentials, shells, databases, or infrastructure.

See SECURITY.md.

## License

MIT
