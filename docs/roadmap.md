# Roadmap

The roadmap stays short. A milestone is complete only when code, tests, and a runnable integration path exist.

## v0.1 — Policy decision boundary

Completed:

- provider-neutral action request;
- Allow, RequireApproval, and Deny;
- deterministic rule precedence;
- conservative default decision;
- HTTP evaluation endpoint;
- unit tests and CI.

The first tagged release remains deferred until the MCP enforcement path is validated.

## v0.2 — MCP stdio proxy

Implementation in review:

- [x] stdio child-process proxy;
- [x] pass-through for non-tool JSON-RPC;
- [x] intercept tools/call;
- [x] evaluate before forwarding;
- [x] stop Deny before upstream execution;
- [x] stop RequireApproval while no approval adapter exists;
- [x] relay upstream responses and errors unchanged;
- [x] fail closed on malformed JSON/tool-call envelopes;
- [x] integration-style tests with an in-memory upstream;
- [x] documented command using the official filesystem MCP server;
- [ ] full pull-request CI validation.

No persistence, dashboard, or hosted service belongs in this milestone.

## v0.3 — Decision receipts

Goal: make decisions reviewable without storing unnecessary sensitive payloads.

Planned separately:

- local SQLite storage;
- bounded decision metadata;
- session/correlation lookup;
- export/query path;
- retention controls;
- tests proving raw prompts and complete tool arguments are excluded by default.

## Later

Only after real usage:

- approval adapters;
- OpenTelemetry;
- agent-specific setup helpers;
- richer resource policies;
- argument inspection/redaction;
- replay;
- UI.

Multi-tenant SaaS control planes, anomaly scoring, and model-based policy decisions remain out of scope until there is evidence they are needed.
