# Runnable local approval demo

This demo exercises the actual stdio MCP proxy, policy evaluator, loopback approval server, bearer authentication, and an in-repository JSON-RPC upstream process.

It does not require an external MCP package.

## What the demo shows

| Tool | Policy result | Upstream execution |
|---|---|---|
| read_file | Allow | immediate |
| write_file | RequireApproval | only after approval |
| delete_file | Deny | never |

The demo upstream prints a result only when the original tools/call actually reaches it.

## Build

~~~bash
dotnet build AgentRuntimeGuard.sln
~~~

## Allow path

Linux/macOS:

~~~bash
cat examples/approval-demo/read-file.json | \
  dotnet run --project src/AgentRuntimeGuard.McpProxy -- \
  --policy examples/policies/approval-demo.json -- \
  dotnet run --project examples/AgentRuntimeGuard.DemoMcpServer
~~~

PowerShell:

~~~powershell
Get-Content examples/approval-demo/read-file.json -Raw |
  dotnet run --project src/AgentRuntimeGuard.McpProxy -- `
  --policy examples/policies/approval-demo.json -- `
  dotnet run --project examples/AgentRuntimeGuard.DemoMcpServer
~~~

Expected result contains:

~~~text
demo upstream executed: read_file
~~~

## Deny path

Replace the request with delete-file.json. The proxy returns a local policy error and the demo upstream never executes delete_file.

## Human approval path

Use two terminals.

### Terminal 1 — start the guarded request

Linux/macOS:

~~~bash
cat examples/approval-demo/write-file.json | \
  dotnet run --project src/AgentRuntimeGuard.McpProxy -- \
  --policy examples/policies/approval-demo.json \
  --approval-port 5087 \
  --approval-timeout-seconds 120 -- \
  dotnet run --project examples/AgentRuntimeGuard.DemoMcpServer
~~~

PowerShell:

~~~powershell
Get-Content examples/approval-demo/write-file.json -Raw |
  dotnet run --project src/AgentRuntimeGuard.McpProxy -- `
  --policy examples/policies/approval-demo.json `
  --approval-port 5087 `
  --approval-timeout-seconds 120 -- `
  dotnet run --project examples/AgentRuntimeGuard.DemoMcpServer
~~~

The proxy prints a generated bearer token once to stderr and waits for approval. Copy that token.

### Terminal 2 — inspect and approve

Linux/macOS:

~~~bash
export ARG_APPROVAL_TOKEN="<token from terminal 1>"
curl -s -H "Authorization: Bearer $ARG_APPROVAL_TOKEN" http://127.0.0.1:5087/v1/approvals
~~~

Copy the returned decisionId, then:

~~~bash
curl -i -X POST -H "Authorization: Bearer $ARG_APPROVAL_TOKEN" http://127.0.0.1:5087/v1/approvals/<decision-id>/approve
~~~

PowerShell:

~~~powershell
$env:ARG_APPROVAL_TOKEN = "<token from terminal 1>"
$pending = Invoke-RestMethod -Headers @{ Authorization = "Bearer $env:ARG_APPROVAL_TOKEN" } -Uri http://127.0.0.1:5087/v1/approvals
$pending
Invoke-RestMethod -Method Post -Headers @{ Authorization = "Bearer $env:ARG_APPROVAL_TOKEN" } -Uri "http://127.0.0.1:5087/v1/approvals/$($pending[0].decisionId)/approve"
~~~

After approval, Terminal 1 completes with a response containing:

~~~text
demo upstream executed: write_file
~~~

To prove rejection, repeat the flow and POST to /v1/approvals/<decision-id>/reject. The proxy returns a local rejection error and the upstream does not execute the tool.

## Data minimization

The pending approval endpoint contains bounded tool/rule/session metadata and expiry timestamps. It does not expose the full MCP arguments object, so the content value in write-file.json does not appear in the pending approval response.

## Security boundary

This is a local developer demo. The approval endpoint binds to IPv4 loopback and requires a bearer token, but the project does not claim OS-user isolation, remote identity, sandboxing, or tamper-evident approval audit.
