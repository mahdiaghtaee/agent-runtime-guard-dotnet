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

This milestone intentionally adds no persistence, dashboard, hosted service, or argument-level inspection.

## Milestone 3 — Decision receipts

Goal: make decisions reviewable without storing unnecessary sensitive payloads.

Tracked in issue #3.

Planned:

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

## Release principle

The first tagged release should represent a usable enforcement path, not only a policy library. With Milestone 2 complete, the repository is ready for its first pre-1.0 release after merge-to-main validation.
