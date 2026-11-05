# Cursor MCP setup

This preset puts Agent Runtime Guard in front of the official filesystem MCP server, so Cursor launches the guard rather than the upstream server directly.

| Tool class | Guard decision |
|---|---|
| read, list, search, metadata, directory tree | Allow |
| write, edit, append, create, copy | RequireApproval |
| move, delete, remove | Deny |
| anything else | RequireApproval |

The policy is `examples/policies/cursor-workspace.json`.

## Requirements

- .NET 10 SDK
- Node.js 24 or later for the current `@modelcontextprotocol/server-filesystem` package
- Cursor with MCP support
- a local clone of this repository

Cursor reads project MCP configuration from `.cursor/mcp.json` and supports environment-variable and workspace-folder interpolation in command arguments.

Official references:

- https://cursor.com/docs/mcp
- https://github.com/modelcontextprotocol/servers/tree/main/src/filesystem

## 1. Build the guard

~~~bash
dotnet build AgentRuntimeGuard.sln
~~~

## 2. Set two local environment variables

`ARG_GUARD_REPO` is the absolute path to this repository.

`ARG_APPROVAL_TOKEN` is a local bearer token for the loopback approval endpoint. Use a random value of at least 32 characters and do not commit it.

### Windows

~~~powershell
setx ARG_GUARD_REPO "C:/src/agent-runtime-guard-dotnet"
setx ARG_APPROVAL_TOKEN "<random-secret-at-least-32-characters>"
~~~

Restart Cursor after using `setx` so it receives the new environment.

### macOS / Linux

~~~bash
export ARG_GUARD_REPO="$HOME/src/agent-runtime-guard-dotnet"
export ARG_APPROVAL_TOKEN="<random-secret-at-least-32-characters>"
~~~

Persist those exports in your shell profile if you want Cursor launches to inherit them consistently.

## 3. Copy the preset into the workspace you want to guard

Create `.cursor/mcp.json` in the target project.

Windows:

~~~text
examples/client-presets/cursor/windows.mcp.json
~~~

macOS/Linux:

~~~text
examples/client-presets/cursor/unix.mcp.json
~~~

The launch chain is:

~~~text
Cursor
  -> AgentRuntimeGuard.McpProxy
     -> @modelcontextprotocol/server-filesystem
        -> current Cursor workspace
~~~

The upstream filesystem server is limited to `${workspaceFolder}`. The guard evaluates policy before forwarding a `tools/call`.

## 4. Restart Cursor and verify the server

The preset registers `guarded-filesystem`.

If it does not connect, open Cursor's MCP logs and verify that `ARG_GUARD_REPO` is correct, `ARG_APPROVAL_TOKEN` is available to Cursor, `dotnet`/`node`/`npx` are on PATH, and the solution builds locally.

## 5. Exercise all three decisions

### Allow

Ask Cursor to list the workspace or read a file through `guarded-filesystem`. Read/list/search-style tools are forwarded immediately.

### RequireApproval

Ask Cursor to create or edit a small test file. The MCP call remains blocked until the local request is approved.

PowerShell:

~~~powershell
$headers = @{ Authorization = "Bearer $env:ARG_APPROVAL_TOKEN" }
$pending = Invoke-RestMethod -Headers $headers -Uri http://127.0.0.1:5087/v1/approvals
$pending | Format-Table decisionId, toolName, operation, resource, expiresAtUtc

Invoke-RestMethod -Method Post -Headers $headers `
  -Uri "http://127.0.0.1:5087/v1/approvals/$($pending[0].decisionId)/approve"
~~~

macOS/Linux:

~~~bash
curl -s -H "Authorization: Bearer $ARG_APPROVAL_TOKEN" http://127.0.0.1:5087/v1/approvals
~~~

Then approve the selected decision:

~~~bash
curl -i -X POST -H "Authorization: Bearer $ARG_APPROVAL_TOKEN" http://127.0.0.1:5087/v1/approvals/<decision-id>/approve
~~~

Use `/reject` instead of `/approve` to reject the request. Rejected requests are not forwarded upstream.

### Deny

Ask Cursor to move or rename a file through the guarded filesystem server. The policy returns Deny locally and the original tool call is not forwarded upstream.

## What this preset proves

Cursor remains the MCP client, while Agent Runtime Guard is a separate enforcement hop between the client and the upstream server. The proxy decides whether the original `tools/call` is forwarded at all.

This is still a deliberately narrow boundary:

- it only protects MCP traffic routed through this proxy;
- it does not sandbox Cursor or the upstream process;
- it does not prevent another unguarded MCP configuration from being added;
- the approval endpoint is loopback-only bearer authentication, not OS-user isolation;
- the preset is a developer integration example, not a production security profile.

## Customize the policy

Copy `examples/policies/cursor-workspace.json` and change the rules for your workspace. A new tool name that does not match an explicit rule falls back to `RequireApproval` rather than being silently allowed.
