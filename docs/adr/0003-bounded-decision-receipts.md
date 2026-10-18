# ADR 0003: Store bounded decision receipts only when explicitly enabled

- Status: Accepted
- Date: 2026-10-18

## Context

After the MCP proxy established a real pre-execution boundary, the next useful debugging capability is to answer simple questions such as:

- what did policy decide during a session;
- which rule matched;
- which tool name was involved;
- in what order did decisions occur.

Persisting the complete policy request or MCP payload would also capture data the project does not need, including prompts or tool arguments.

## Decision

Decision receipts are optional and disabled by default.

When enabled, the guard stores a bounded receipt containing:

- decision ID;
- evaluation timestamp;
- effect;
- matched rule ID;
- agent ID;
- session ID;
- tool name;
- operation;
- resource.

The schema does not include:

- prompt text;
- MCP arguments;
- policy reason;
- tags;
- tool result;
- upstream stderr.

String metadata is clipped to explicit maximum lengths before it is written.

SQLite is used for the first local implementation. The SQLite dependency is isolated in AgentRuntimeGuard.Receipts rather than Core.

## Failure behavior

If receipt persistence is enabled and writing the receipt fails, the decorated evaluator does not return the policy decision.

This is a conservative choice: explicitly enabling receipts means the operator requested a recorded decision path. Continuing with an unrecorded Allow would violate that expectation.

Users who do not want persistence as an execution dependency can omit --receipts entirely.

## Retention

Retention is count-based and deterministic.

After each insert, the store keeps the newest configured maximum number of receipts and removes older rows in the same transaction.

The initial default is 10,000 receipts.

## Query/export

The proxy executable exposes a local receipts command that queries by session ID and emits JSON Lines, newest first.

This keeps the first review surface scriptable and avoids adding a dashboard or web administration surface.

## Security consequences

SQLite data is not encrypted by this project.

The operator remains responsible for filesystem permissions, host access, backup handling, and deletion of the database file.

The receipt database is for local reviewability, not tamper-evident audit or compliance retention.

## Follow-up

Do not add argument-level persistence until there is a separate design for redaction, classification, retention, and access control.
