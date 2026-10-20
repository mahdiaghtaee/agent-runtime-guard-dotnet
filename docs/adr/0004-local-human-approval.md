# ADR 0004: Keep first human approval local, bounded, and fail-closed

- Status: Accepted
- Date: 2026-10-20

## Context

The deterministic policy engine already returns `RequireApproval`, but the MCP proxy previously treated that outcome as an unconditional block.

The next step is a real human-in-the-loop path without introducing a hosted service, account system, dashboard, or remote identity model.

## Decision

Human approval remains optional and outside `AgentRuntimeGuard.Core`.

The first adapter uses:

- `IApprovalProvider`;
- an in-memory `LocalApprovalBroker`;
- a small ASP.NET Core/Kestrel HTTP server;
- direct IPv4 loopback binding;
- bearer-token authentication;
- bounded metadata;
- a finite approval timeout.

Only `Approved` forwards the original MCP request. Rejection, expiry, missing provider, cancellation, invalid credentials, and broker failure remain blocked.

## Token handling

If no explicit token source is configured, the proxy generates 32 cryptographically random bytes and prints the base64url token once to stderr.

Explicit tokens are loaded from a named environment variable. A raw bearer secret is intentionally not accepted as a CLI argument because process command lines are commonly observable.

## Data minimization

Pending approval metadata is limited to decision ID, rule ID, session ID, tool name, operation, resource, and creation/expiry timestamps.

The complete MCP `arguments` object is never copied into the approval provider or HTTP response.

## Consequences

This creates a usable single-operator local approval path while keeping Core deterministic and HTTP-independent.

It does not provide OS-user isolation, remote/mobile approval, user identity, multi-party authorization, or tamper-evident approval audit.

If real usage justifies more work, bounded approval telemetry should come before a dashboard or remote approval surface.
