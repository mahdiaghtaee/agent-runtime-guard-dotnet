using System.Text.Json;
using Xunit;

namespace AgentRuntimeGuard.Tests;

public sealed class CursorPresetContractTests
{
    [Fact]
    public void Cursor_workspace_policy_keeps_conservative_decision_contract()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "examples", "policies", "cursor-workspace.json");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var policy = document.RootElement;

        Assert.Equal("RequireApproval", policy.GetProperty("defaultEffect").GetString());

        var rules = policy.GetProperty("rules")
            .EnumerateArray()
            .ToDictionary(
                rule => rule.GetProperty("id").GetString()!,
                rule => new RuleContract(
                    rule.GetProperty("priority").GetInt32(),
                    rule.GetProperty("effect").GetString()!,
                    rule.GetProperty("toolPattern").GetString()!));

        AssertRule(rules, "allow-workspace-read", 100, "Allow", "read*");
        AssertRule(rules, "allow-workspace-list", 100, "Allow", "list*");
        AssertRule(rules, "allow-workspace-search", 100, "Allow", "search*");
        AssertRule(rules, "allow-workspace-metadata", 100, "Allow", "get*");
        AssertRule(rules, "allow-workspace-tree", 100, "Allow", "directory_tree");

        AssertRule(rules, "approve-workspace-write", 200, "RequireApproval", "write*");
        AssertRule(rules, "approve-workspace-edit", 200, "RequireApproval", "edit*");
        AssertRule(rules, "approve-workspace-append", 200, "RequireApproval", "append*");
        AssertRule(rules, "approve-workspace-create", 200, "RequireApproval", "create*");
        AssertRule(rules, "approve-workspace-copy", 200, "RequireApproval", "copy*");

        AssertRule(rules, "deny-workspace-move", 300, "Deny", "move*");
        AssertRule(rules, "deny-workspace-delete", 300, "Deny", "delete*");
        AssertRule(rules, "deny-workspace-remove", 300, "Deny", "remove*");

        Assert.DoesNotContain(
            rules.Values,
            rule => rule.Effect == "Allow" && rule.ToolPattern is "*" or "**");
    }

    [Fact]
    public void Cursor_presets_keep_guard_between_client_and_upstream_filesystem_server()
    {
        var root = FindRepositoryRoot();

        var windows = ReadServer(
            Path.Combine(root, "examples", "client-presets", "cursor", "windows.mcp.json"));

        Assert.Equal("stdio", windows.GetProperty("type").GetString());
        Assert.Equal("dotnet", windows.GetProperty("command").GetString());

        var windowsArgs = ReadArgs(windows);
        AssertOrdered(
            windowsArgs,
            "${env:ARG_GUARD_REPO}/src/AgentRuntimeGuard.McpProxy/AgentRuntimeGuard.McpProxy.csproj",
            "--policy",
            "${env:ARG_GUARD_REPO}/examples/policies/cursor-workspace.json",
            "--approval-port",
            "5087",
            "--approval-token-env",
            "ARG_APPROVAL_TOKEN",
            "--",
            "cmd",
            "/c",
            "npx",
            "-y",
            "@modelcontextprotocol/server-filesystem",
            "${workspaceFolder}");

        var unix = ReadServer(
            Path.Combine(root, "examples", "client-presets", "cursor", "unix.mcp.json"));

        Assert.Equal("stdio", unix.GetProperty("type").GetString());
        Assert.Equal("dotnet", unix.GetProperty("command").GetString());

        var unixArgs = ReadArgs(unix);
        AssertOrdered(
            unixArgs,
            "${env:ARG_GUARD_REPO}/src/AgentRuntimeGuard.McpProxy/AgentRuntimeGuard.McpProxy.csproj",
            "--policy",
            "${env:ARG_GUARD_REPO}/examples/policies/cursor-workspace.json",
            "--approval-port",
            "5087",
            "--approval-token-env",
            "ARG_APPROVAL_TOKEN",
            "--",
            "npx",
            "-y",
            "@modelcontextprotocol/server-filesystem",
            "${workspaceFolder}");

        Assert.DoesNotContain("cmd", unixArgs);
        Assert.DoesNotContain("/c", unixArgs);
    }

    private static JsonElement ReadServer(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement
            .GetProperty("mcpServers")
            .GetProperty("guarded-filesystem")
            .Clone();
    }

    private static string[] ReadArgs(JsonElement server) =>
        server.GetProperty("args")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();

    private static void AssertRule(
        IReadOnlyDictionary<string, RuleContract> rules,
        string id,
        int priority,
        string effect,
        string toolPattern)
    {
        Assert.True(rules.TryGetValue(id, out var rule), $"Missing rule '{id}'.");
        Assert.Equal(priority, rule!.Priority);
        Assert.Equal(effect, rule.Effect);
        Assert.Equal(toolPattern, rule.ToolPattern);
    }

    private static void AssertOrdered(IReadOnlyList<string> actual, params string[] expected)
    {
        var searchFrom = 0;

        foreach (var item in expected)
        {
            var index = -1;
            for (var i = searchFrom; i < actual.Count; i++)
            {
                if (actual[i] == item)
                {
                    index = i;
                    break;
                }
            }

            Assert.True(index >= 0, $"Expected argument '{item}' after index {searchFrom - 1}.");
            searchFrom = index + 1;
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgentRuntimeGuard.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate AgentRuntimeGuard.sln from the test output directory.");
    }

    private sealed record RuleContract(int Priority, string Effect, string ToolPattern);
}
