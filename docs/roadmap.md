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

- loopback-only bearer-protected approval server;
- bounded approval metadata;
- configurable timeout;
- explicit approve/reject flow;
- fail-closed reject/expire/provider-failure behavior;
- runnable in-repository demo;
- full CI validation.

## Milestone 5 — Bounded approval telemetry — Completed

Tracked in issue #21.

- [x] provider decorator outside Core;
- [x] `ActivitySource` approval wait trace;
- [x] completed-outcome counter;
- [x] approval-duration histogram;
- [x] bounded outcome vocabulary;
- [x] no decision/session/tool/resource tags;
- [x] cancellation and provider-error observability;
- [x] no OpenTelemetry SDK/exporter dependency;
- [x] telemetry data-minimization tests;
- [x] full pull-request CI validation.

Non-goals: collector deployment, dashboard, Prometheus endpoint, hosted telemetry backend, per-tool/high-cardinality metrics, or production monitoring claims.

## Milestone 6 — Guarded Cursor setup — Completed

- project-level Cursor MCP presets for Windows and macOS/Linux;
- guard proxy launched before the upstream filesystem MCP server;
- conservative workspace policy with explicit Allow / RequireApproval / Deny classes;
- unknown tools default to `RequireApproval`;
- local approval flow documented for Cursor users;
- CI contract tests for policy posture and launch-chain ordering.

## Later

- additional client-specific setup helpers when real usage justifies them;
- richer resource policies;
- argument inspection/redaction;
- replay;
- UI.
