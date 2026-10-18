# Roadmap

The roadmap stays short. A milestone is complete only when code, tests, and a runnable integration path exist.

## Milestone 1 — Policy decision boundary — Completed

Delivered:

- provider-neutral action request;
- Allow, RequireApproval, and Deny;
- deterministic rule precedence;
- conservative default decision;
- HTTP evaluation endpoint;
- unit tests and CI.

## Milestone 2 — MCP stdio proxy — Completed

Delivered:

- stdio child-process proxy;
- pass-through for non-tool JSON-RPC;
- interception of tools/call;
- policy evaluation before forwarding;
- Deny blocked before upstream execution;
- RequireApproval blocked while no approval adapter exists;
- unchanged relay of upstream responses and errors;
- fail-closed handling for malformed JSON/tool-call envelopes;
- integration-style tests proving allowed, denied, pass-through, and upstream-error behavior;
- documented command using the official filesystem MCP server;
- full pull-request CI validation.

## Milestone 3 — Decision receipts — Completed

Tracked in issue #3.

Delivered:

- [x] optional SQLite persistence;
- [x] bounded decision metadata;
- [x] session/correlation lookup;
- [x] JSONL query/export command;
- [x] deterministic maximum-count retention;
- [x] schema tests excluding prompt, arguments, reason, and tags;
- [x] Allow, RequireApproval, and Deny receipt coverage;
- [x] fail-closed behavior when enabled persistence fails;
- [x] full pull-request CI validation.

This milestone deliberately adds no cloud backend, analytics dashboard, anomaly scoring, multi-tenant administration, or database encryption.

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
