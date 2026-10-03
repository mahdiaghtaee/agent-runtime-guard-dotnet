# Security

## Current status

Agent Runtime Guard is an early engineering project. It is not a production security boundary by itself.

## Important assumptions

The guard only has enforcement value when the protected execution path cannot bypass it.

The current v0.1 scope does not provide:

- process sandboxing;
- OS-level command isolation;
- credential brokering;
- network egress enforcement;
- persistent tamper-evident audit storage;
- authenticated policy administration;
- multi-tenant isolation.

Do not place production secrets or irreversible operations behind this service as the only control.

## Sensitive data

The evaluation contract intentionally does not require raw prompts or complete tool argument payloads. Callers should prefer bounded metadata and resource identifiers over copying secret-bearing payloads into policy requests.

## Reporting

If you find a security problem, avoid publishing secrets, credentials, or exploit data from a real system in a public issue. A private reporting channel can be added before the first public release if the repository receives external usage.
