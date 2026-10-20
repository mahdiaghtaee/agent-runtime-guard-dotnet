# Local Human Approval Demo

This demo shows the security-relevant behavior added after v0.2.0:

> a tool call that receives `RequireApproval` does not reach the upstream MCP server until a local human explicitly approves it.

## 1. Start the guarded filesystem server

Use the demo policy:

~~~bash
dotnet run --project src/AgentRuntimeGuard.McpProxy -- \
  --policy examples/policies/approval-demo.json \
  --approval-port 5087 \
  --approval-timeout-seconds 120 \
  -- npx -y @modelcontextprotocol/server-filesystem .
~~~

On Windows:

~~~powershell
dotnet run --project src/AgentRuntimeGuard.McpProxy -- `
  --policy examples/policies/approval-demo.json `
  --approval-port 5087 `
  --approval-timeout-seconds 120 `
  -- cmd /c npx -y @modelcontextprotocol/server-filesystem .
~~~

The proxy prints the generated bearer token to **stderr** once at startup.

For repeatable local demos, set your own token through an environment variable and add `--approval-token-env ARG_APPROVAL_TOKEN`.

~~~bash
export ARG_APPROVAL_TOKEN="replace-with-a-long-local-demo-token-1234"
~~~

## 2. Point an MCP client at the proxy

Configure a stdio MCP client to launch the guard command above instead of launching the filesystem server directly.

The demo policy has three paths:

| Tool pattern | Policy result | Runtime behavior |
|---|---|---|
| `read*`, `list*` | `Allow` | forwarded immediately |
| `write*`, `edit*`, `delete*` | `Deny` | never forwarded |
| `create_directory`, `move_file`, other unmatched tools | `RequireApproval` | waits for local approval |

Trigger a filesystem operation such as `create_directory`.

## 3. Inspect the pending decision

~~~bash
curl -H "Authorization: Bearer $ARG_APPROVAL_TOKEN" \
  http://127.0.0.1:5087/v1/approvals
~~~

The response contains bounded metadata only. Raw MCP arguments are intentionally absent.

## 4. Approve or reject

Approve:

~~~bash
curl -X POST \
  -H "Authorization: Bearer $ARG_APPROVAL_TOKEN" \
  http://127.0.0.1:5087/v1/approvals/<decision-id>/approve
~~~

The original MCP request is then forwarded unchanged.

Reject:

~~~bash
curl -X POST \
  -H "Authorization: Bearer $ARG_APPROVAL_TOKEN" \
  http://127.0.0.1:5087/v1/approvals/<decision-id>/reject
~~~

The upstream MCP server never receives the rejected request. If no decision is made before the configured timeout, the call expires and remains blocked.

## 5. Verify the behavior in CI

~~~bash
dotnet test AgentRuntimeGuard.sln --configuration Release
~~~

Coverage includes:

- approved `RequireApproval` calls reaching upstream;
- rejected and expired calls never reaching upstream;
- missing approval provider remaining fail-closed;
- approval-provider exceptions remaining fail-closed;
- missing/wrong bearer tokens failing authorization;
- pending approval JSON excluding raw MCP arguments;
- loopback-only listener binding.

## Trust boundary

This is a **single-machine, single-operator** approval path.

It is not a remote identity system, OS sandbox, multi-user authorization service, or tamper-evident approval audit. A sufficiently privileged same-host process remains outside this boundary.