# Roadmap

The roadmap is intentionally short. A milestone is only marked complete when code, tests, and a runnable example exist.

## v0.1 — Policy decision boundary

Goal: define and validate the smallest useful pre-execution policy model.

- [x] provider-neutral action request;
- [x] `Allow`, `RequireApproval`, and `Deny`;
- [x] deterministic rule precedence;
- [x] conservative default decision;
- [x] HTTP evaluation endpoint;
- [x] unit tests and CI;
- [ ] publish the first tagged release after the repository is exercised outside the test project.

## v0.2 — MCP stdio proxy

Goal: route one real MCP execution path through policy evaluation.

Planned:

- MCP stdio client/server proxy;
- pass-through tool discovery;
- intercept `tools/call`;
- evaluate before forwarding;
- return a clear protocol error on denied actions;
- preserve upstream error behavior;
- integration test with a small local MCP server.

No dashboard is required for this milestone.

## v0.3 — Decision receipts

Goal: make decisions reviewable without storing unnecessary sensitive payloads.

Candidate work:

- local SQLite decision log;
- bounded metadata;
- correlation/session identifiers;
- query/export command;
- retention controls;
- explicit redaction tests.

## Later

Only after real usage:

- approval adapters;
- OpenTelemetry;
- agent-specific setup helpers;
- richer resource policies;
- argument inspection/redaction;
- replay;
- UI.

Multi-tenant control planes, hosted SaaS features, anomaly scoring, and model-based policy decisions are explicitly out of scope until there is evidence they are needed.
