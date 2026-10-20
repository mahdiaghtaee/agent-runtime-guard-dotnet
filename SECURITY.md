# Security

## Current status

Agent Runtime Guard is an early engineering project. It is not a production security boundary by itself.

## Important assumptions

The guard only has enforcement value when the protected execution path cannot bypass it.

The current project does not provide:

- process sandboxing;
- OS-level command isolation;
- credential brokering;
- network egress enforcement;
- remote identity or multi-user approval authorization;
- tamper-evident approval audit;
- persistent tamper-evident audit storage;
- multi-tenant isolation.

Do not place production secrets or irreversible operations behind this service as the only control.

## Local approval boundary

Human approval is disabled unless explicitly enabled.

When enabled:

- the HTTP listener binds only to IPv4 loopback;
- approval routes require a bearer token;
- generated tokens use cryptographically secure random bytes;
- explicit tokens are loaded from an environment variable;
- rejected, expired, unauthenticated, and broker-failed requests do not forward the tool call;
- raw MCP arguments are not exposed through the approval API.

The bearer token is not OS-user isolation. A sufficiently privileged same-host process remains outside this boundary. Generated tokens are written once to stderr for the local operator, so terminal/log capture must be treated accordingly.

## Sensitive data

The evaluation contract intentionally does not require raw prompts or complete tool argument payloads. Callers should prefer bounded metadata and resource identifiers over copying secret-bearing payloads into policy requests.

## Reporting

If you find a security problem, avoid publishing secrets, credentials, or exploit data from a real system in a public issue. A private reporting channel can be added before the first public release if the repository receives external usage.
