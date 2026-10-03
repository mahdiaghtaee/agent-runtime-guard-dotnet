# Agent Runtime Guard for .NET

[![CI](https://github.com/mahdiaghtaee/agent-runtime-guard-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/mahdiaghtaee/agent-runtime-guard-dotnet/actions/workflows/ci.yml)

A small .NET service for evaluating AI-agent tool actions before execution.

The first version focuses on one boundary: an agent proposes a tool action, the guard evaluates local policy, and the caller receives one of three decisions:

- `Allow`
- `RequireApproval`
- `Deny`

The guard does not execute the tool itself. That separation is intentional.

> **Status:** early v0.1 foundation. This is an engineering experiment, not a production security product.

## Why this project exists

Coding agents and MCP-enabled applications increasingly call shells, databases, file systems, and external services. Prompt-level instructions can influence behavior, but they are not an independent enforcement boundary.

This repository explores a narrower question:

> Can a tool call be evaluated by a separate, deterministic policy component before a side effect occurs?

The first milestone is deliberately small so the behavior can be explained, tested, and reviewed without a large framework around it.

## Current scope

The v0.1 branch introduces:

- provider-neutral tool-action request models;
- deterministic rule evaluation;
- explicit allow / approval / deny outcomes;
- priority-based rule selection;
- wildcard matching for tool, operation, and resource;
- a conservative configurable default decision;
- a minimal ASP.NET Core evaluation endpoint;
- unit tests and CI;
- an ADR describing the trust boundary.

Not implemented yet:

- MCP proxying;
- approval workflow;
- persistent decision receipts;
- replay;
- agent-specific adapters;
- command-argument inspection;
- secret detection;
- identity or multi-tenant policy;
- a dashboard.

Those are deferred until the decision model is exercised in a real integration.

## Quick start

Requirements:

- .NET 10 SDK

Run the API:

```bash
dotnet run --project src/AgentRuntimeGuard.Api/AgentRuntimeGuard.Api.csproj
```

Then evaluate an action:

```bash
curl -X POST http://localhost:5078/v1/evaluate \
  -H "Content-Type: application/json" \
  -d '{
    "agentId": "local-coding-agent",
    "sessionId": "session-001",
    "toolName": "filesystem",
    "operation": "read",
    "resource": "src/Program.cs",
    "tags": ["workspace"]
  }'
```

## Policy behavior

Rules live in `src/AgentRuntimeGuard.Api/appsettings.json`.

A rule contains:

```json
{
  "id": "review-database-writes",
  "priority": 200,
  "effect": "RequireApproval",
  "toolPattern": "database",
  "operationPattern": "write",
  "resourcePattern": "*",
  "reason": "Database writes require an explicit approval step."
}
```

Matching is case-insensitive and supports `*` as a simple wildcard.

When multiple rules match:

1. higher priority wins;
2. at the same priority, the more restrictive effect wins (`Deny` > `RequireApproval` > `Allow`);
3. rule ID provides deterministic final ordering.

If no rule matches, the configured default is returned. The starter default is `RequireApproval`.

## Repository layout

```text
src/
  AgentRuntimeGuard.Abstractions/
  AgentRuntimeGuard.Core/
  AgentRuntimeGuard.Api/

tests/
  AgentRuntimeGuard.Tests/

docs/
  adr/
  architecture.md
  roadmap.md
```

- **Abstractions** owns the public decision contract.
- **Core** owns deterministic policy evaluation.
- **API** owns transport and configuration.
- **Tests** validate behavior without external services.

## Trust boundary

The guard can only enforce actions that actually pass through it.

If an agent can bypass the guard and call a shell, database, or MCP server directly, the policy has no enforcement value. Integration placement is therefore part of the security model.

See [ADR 0001](docs/adr/0001-policy-before-execution.md).

## Design principles

- keep the first enforcement boundary deterministic;
- avoid provider-specific agent contracts in the core;
- prefer explicit decisions over opaque risk scores;
- fail conservatively when policy does not match;
- do not persist raw prompts or tool arguments by default;
- add adapters only after the protocol boundary is understood;
- document limitations next to capabilities.

## Roadmap

See [docs/roadmap.md](docs/roadmap.md).

The next meaningful milestone is an MCP stdio proxy that can route tool calls through this evaluator without changing the upstream MCP server.

## Security

This repository is experimental. Do not use it as the only control protecting production credentials, shells, databases, or infrastructure.

See [SECURITY.md](SECURITY.md).

## License

MIT
