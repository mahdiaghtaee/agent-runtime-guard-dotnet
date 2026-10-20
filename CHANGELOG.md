# Changelog

All notable changes to this project are documented here.

## Unreleased

### Added

- Optional local human approval for `RequireApproval` MCP tool calls.
- Loopback-only bearer-protected pending-approval HTTP API.
- Generated or environment-supplied approval bearer tokens.
- Configurable approval timeout and bounded pending metadata.
- End-to-end tests for approved, rejected, expired, missing-provider, and invalid-token paths.

### Security

- Raw MCP arguments are not copied into approval requests or approval HTTP responses.
- Approval routes bind only to IPv4 loopback.
- Rejection, timeout, missing provider, and broker failure prevent upstream execution.
- Explicit approval tokens are read from an environment variable rather than accepted as a raw command-line secret.

## 0.2.0 - 2026-10-18

### Added

- Opt-in local SQLite decision receipts.
- Session-scoped receipt query/export as JSON Lines.
- Deterministic maximum-count retention.
- Receipt metadata length bounds.
- Tests for Allow, RequireApproval, Deny, retention, session filtering, schema data minimization, MCP-to-receipt integration, and CLI export.

### Changed

- When receipt persistence is explicitly enabled, a persistence failure prevents returning an unrecorded policy decision.
- SQLite persistence remains isolated from the policy Core and MCP protocol model.

### Security

- Receipt schema excludes raw prompts, MCP arguments, policy reasons, tags, tool results, and upstream stderr.
- SQLite files are local and unencrypted; filesystem protection remains an operator responsibility.
- Receipt persistence remains opt-in and disabled on the default MCP execution path.

## 0.1.0 - 2026-10-09

### Added

- Provider-neutral tool-action policy contracts.
- Deterministic Allow, RequireApproval, and Deny decisions.
- Priority-based wildcard policy matching with a conservative default.
- ASP.NET Core policy-evaluation endpoint.
- Stdio MCP child-process proxy.
- Pre-execution interception of MCP tools/call requests.
- Pass-through behavior for non-tool JSON-RPC traffic.
- Local JSON-RPC policy errors for blocked and approval-required calls.
- Fail-closed handling for malformed client JSON and malformed tool-call envelopes.
- Integration-style tests proving denied calls never reach upstream input.
- Tests covering allowed calls, upstream errors, non-tool pass-through, and argument redaction from local block responses.
- Example read-oriented policy for the official filesystem MCP server.
- CI, security notes, architecture documentation, and ADRs for the enforcement boundary.

### Known limitations

- RequireApproval currently blocks because no approval adapter exists.
- Tool arguments are not inspected by policy.
- Decisions are not persisted.
- The proxy does not provide process sandboxing, network isolation, credential brokering, or protection against bypassing the proxy.
- This release is an early engineering reference, not a production security product.
