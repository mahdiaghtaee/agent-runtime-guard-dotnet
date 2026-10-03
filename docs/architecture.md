# Architecture

## Initial boundary

The first version has one job: turn a proposed tool action into a policy decision.

```text
Agent / MCP client
        |
        | proposed action
        v
+------------------------+
| Agent Runtime Guard    |
|                        |
| request validation     |
| deterministic matching |
| explicit decision      |
+------------------------+
        |
        +--> Allow
        +--> RequireApproval
        +--> Deny
```

The guard does not execute the action.

That separation keeps policy evaluation and side-effect execution as different failure domains.

## Projects

### AgentRuntimeGuard.Abstractions

Transport-neutral contracts:

- `ToolActionRequest`
- `PolicyEffect`
- `PolicyEvaluationResult`
- `IPolicyEvaluator`

This project should stay free of ASP.NET Core, MCP, database, or provider dependencies.

### AgentRuntimeGuard.Core

Deterministic policy behavior:

- rule model;
- wildcard matcher;
- priority and restriction ordering;
- conservative default decision.

The core should remain usable from HTTP, MCP, CLI, or an embedded adapter.

### AgentRuntimeGuard.Api

ASP.NET Core transport and configuration.

The HTTP API is an integration boundary, not the domain model.

## Data minimization

The request model does not include raw prompts or complete tool argument payloads.

The current fields are enough to test policy shape:

- caller-supplied agent and session identifiers;
- tool;
- operation;
- resource;
- bounded tags.

Future argument inspection should be introduced only with an explicit retention and redaction design.

## Enforcement limitation

A policy decision is not enforcement unless the execution path is forced through the guard.

The project will not claim otherwise.

The MCP proxy milestone is intended to create one concrete enforcement path that can be demonstrated end to end.
