# ADR 0001: Keep policy evaluation before execution

- Status: Accepted
- Date: 2026-10-03

## Context

An AI agent can propose actions with real side effects: file writes, shell commands, database mutations, network calls, or MCP tool invocations.

A prompt-level instruction can influence behavior, but it is not an independent enforcement boundary.

The first design question is where policy belongs.

## Decision

Agent Runtime Guard evaluates an action before the protected side effect is executed.

The initial core returns one explicit effect:

- `Allow`
- `RequireApproval`
- `Deny`

The evaluator does not execute the requested tool.

Transport adapters are responsible for placing the evaluator in the actual execution path.

## Consequences

Positive:

- policy behavior can be tested without an agent or external model;
- the core stays provider-neutral;
- enforcement and execution failures remain separate;
- callers can decide how approval is implemented.

Negative:

- integrations must be designed so the guard cannot be trivially bypassed;
- the HTTP endpoint alone does not create enforcement;
- some policies will eventually need more context than the v0.1 request model carries.

## Follow-up

The next milestone is an MCP stdio proxy. It should demonstrate one end-to-end path where a denied `tools/call` is not forwarded upstream.
