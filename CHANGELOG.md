# Changelog

All notable changes to this project are documented here.

## Unreleased

No active release changes yet.

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
