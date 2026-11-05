# ADR 0005: Keep approval telemetry bounded and exporter-neutral

- Status: Accepted
- Date: 2026-11-05

## Context

Local human approval creates a useful operational question: how often approvals complete, with what outcome, and how long a human decision takes.

Adding per-tool/session/decision telemetry would make debugging easy but creates high-cardinality labels and can copy identifying or sensitive operational metadata into observability systems.

## Decision

Approval telemetry is implemented as an `IApprovalProvider` decorator using only .NET BCL diagnostics:

- `ActivitySource` for one `approval.wait` activity;
- `Meter` counter for completed approvals;
- `Meter` histogram for approval wait duration.

No OpenTelemetry SDK or exporter package is required by the project.

## Allowed telemetry data

The only dimension/tag is bounded approval outcome:

- `approved`;
- `rejected`;
- `expired`;
- `cancelled`;
- `error`.

## Excluded telemetry data

Do not attach:

- decision ID;
- session ID;
- rule ID;
- tool name;
- operation;
- resource;
- MCP arguments;
- bearer token;
- prompt;
- tool result.

## Consequences

An OpenTelemetry-compatible host can subscribe to the stable source/meter name without changing the guard, while default execution sends nothing over the network.

The tradeoff is intentional: metrics cannot answer which specific tool/session caused latency or rejection. Detailed local review remains the responsibility of bounded decision receipts and approval state, not metric labels.

## Follow-up

Do not add an exporter or dashboard until an actual deployment demonstrates a concrete need.
