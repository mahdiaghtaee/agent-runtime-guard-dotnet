# Roadmap

The roadmap stays short. A milestone is complete only when code, tests, and a runnable integration path exist.

## Milestone 1 — Policy decision boundary — Completed

- provider-neutral action request;
- `Allow`, `RequireApproval`, and `Deny`;
- deterministic precedence and conservative default;
- HTTP evaluation endpoint;
- tests and CI.

## Milestone 2 — MCP stdio proxy — Completed

- intercept `tools/call` before upstream execution;
- pass non-tool JSON-RPC through unchanged;
- block denied/malformed calls;
- preserve upstream responses and errors;
- integration-style tests.

## Milestone 3 — Decision receipts — Completed

- optional SQLite persistence;
- bounded decision metadata;
- session query/export as JSONL;
- deterministic retention;
- schema data-minimization tests;
- fail-closed persistence behavior.

## Milestone 4 — Local human approval — Completed

Tracked in issue #11.

- [x] approval abstraction outside Core;
- [x] opt-in loopback-only HTTP approval server;
- [x] generated or environment-supplied bearer token;
- [x] bounded pending-approval metadata;
- [x] configurable timeout;
- [x] approved `RequireApproval` calls forward unchanged;
- [x] reject/expire/broker-failure paths remain blocked;
- [x] no-provider behavior remains fail-closed;
- [x] invalid/missing bearer credentials cannot approve;
- [x] raw MCP arguments are absent from approval responses;
- [x] tests prove rejected/expired calls do not reach upstream;
- [x] full pull-request CI validation.

Non-goals: public listener, dashboard, user accounts, remote IdP, multi-tenant approval administration, and production audit/compliance claims.

## Later

- bounded OpenTelemetry approval metrics/traces;
- agent-specific setup helpers;
- richer resource policies;
- argument inspection/redaction;
- replay;
- UI.
