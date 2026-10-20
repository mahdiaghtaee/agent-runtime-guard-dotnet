using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AgentRuntimeGuard.Abstractions;
using AgentRuntimeGuard.Approvals;
using AgentRuntimeGuard.Core;
using AgentRuntimeGuard.McpProxy;

namespace AgentRuntimeGuard.Tests;

public sealed class LocalApprovalServerTests
{
    [Fact]
    public async Task Server_binds_to_loopback_and_requires_bearer_token()
    {
        var broker = new LocalApprovalBroker(TimeSpan.FromSeconds(5));
        const string token =
            "test-token-that-is-long-enough-for-local-approval";

        await using var server = await LocalApprovalServer.StartAsync(
            broker,
            token,
            port: 0);

        Assert.True(
            IPAddress.TryParse(
                server.BaseAddress.Host,
                out var address));
        Assert.True(IPAddress.IsLoopback(address));

        using var client = new HttpClient
        {
            BaseAddress = server.BaseAddress
        };

        var missing = await client.GetAsync("/v1/approvals");
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "wrong-token");

        var wrong = await client.GetAsync("/v1/approvals");
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var accepted = await client.GetAsync("/v1/approvals");
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task Pending_endpoint_excludes_raw_mcp_arguments_and_valid_approval_forwards()
    {
        var broker = new LocalApprovalBroker(TimeSpan.FromSeconds(5));
        const string token =
            "test-token-that-is-long-enough-for-local-approval";

        await using var server = await LocalApprovalServer.StartAsync(
            broker,
            token,
            port: 0);

        var evaluator = new PolicyEvaluator(
            new PolicyRuleSet
            {
                DefaultEffect = PolicyEffect.RequireApproval,
                DefaultReason = "Approval required."
            });

        var gate = new McpToolCallPolicyGate(
            evaluator,
            "approval-http-session",
            broker);

        var gateTask = gate.EvaluateAsync(
            """{"jsonrpc":"2.0","id":30,"method":"tools/call","params":{"name":"write_file","arguments":{"secret":"super-secret-value","path":"notes.txt"}}}""",
            CancellationToken.None).AsTask();

        await WaitForPendingAsync(broker);

        using var client = CreateAuthorizedClient(
            server.BaseAddress,
            token);

        var pendingResponse = await client.GetAsync("/v1/approvals");
        var body = await pendingResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);
        Assert.DoesNotContain("super-secret-value", body, StringComparison.Ordinal);
        Assert.DoesNotContain("arguments", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", body, StringComparison.OrdinalIgnoreCase);

        using var document = JsonDocument.Parse(body);
        var pending = Assert.Single(document.RootElement.EnumerateArray().ToArray());

        Assert.Equal(
            "write_file",
            pending.GetProperty("toolName").GetString());
        Assert.Equal(
            "approval-http-session",
            pending.GetProperty("sessionId").GetString());

        var decisionId = pending.GetProperty("decisionId").GetGuid();

        using var wrongClient = new HttpClient
        {
            BaseAddress = server.BaseAddress
        };
        wrongClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "wrong-token");

        var wrongApproval = await wrongClient.PostAsync(
            $"/v1/approvals/{decisionId}/approve",
            content: null);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            wrongApproval.StatusCode);
        Assert.Single(broker.GetPending());

        var approve = await client.PostAsync(
            $"/v1/approvals/{decisionId}/approve",
            content: null);

        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);

        var result = await gateTask;
        Assert.True(result.Forward);
    }

    [Fact]
    public async Task Rejection_completes_pending_request_without_approval()
    {
        var broker = new LocalApprovalBroker(TimeSpan.FromSeconds(5));
        const string token =
            "test-token-that-is-long-enough-for-local-approval";

        await using var server = await LocalApprovalServer.StartAsync(
            broker,
            token,
            port: 0);

        var request = Request(Guid.NewGuid());
        var pendingTask = broker.RequestApprovalAsync(
            request,
            CancellationToken.None).AsTask();

        await WaitForPendingAsync(broker);

        var decisionId = Assert.Single(
            broker.GetPending()).DecisionId;

        using var client = CreateAuthorizedClient(
            server.BaseAddress,
            token);

        var reject = await client.PostAsync(
            $"/v1/approvals/{decisionId}/reject",
            content: null);

        Assert.Equal(HttpStatusCode.NoContent, reject.StatusCode);
        Assert.Equal(
            ApprovalOutcome.Rejected,
            await pendingTask);
    }

    [Fact]
    public async Task Pending_request_expires_fail_closed()
    {
        var broker = new LocalApprovalBroker(
            TimeSpan.FromMilliseconds(50));

        var outcome = await broker.RequestApprovalAsync(
            Request(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(ApprovalOutcome.Expired, outcome);
        Assert.Empty(broker.GetPending());
    }

    [Fact]
    public void Approval_cli_is_opt_in_and_validates_dependent_options()
    {
        var basic = McpProxyCommandLine.TryParse(
            ["--policy", "policy.json", "--", "server"],
            out var basicOptions,
            out var basicError);

        Assert.True(basic);
        Assert.Null(basicError);
        Assert.NotNull(basicOptions);
        Assert.Null(basicOptions.ApprovalPort);

        var invalid = McpProxyCommandLine.TryParse(
            [
                "--policy",
                "policy.json",
                "--approval-timeout-seconds",
                "30",
                "--",
                "server"
            ],
            out _,
            out var invalidError);

        Assert.False(invalid);
        Assert.Contains(
            "--approval-port",
            invalidError,
            StringComparison.Ordinal);

        var configured = McpProxyCommandLine.TryParse(
            [
                "--policy",
                "policy.json",
                "--approval-port",
                "5087",
                "--approval-timeout-seconds",
                "45",
                "--approval-token-env",
                "ARG_APPROVAL_TOKEN",
                "--",
                "server"
            ],
            out var configuredOptions,
            out var configuredError);

        Assert.True(configured);
        Assert.Null(configuredError);
        Assert.NotNull(configuredOptions);
        Assert.Equal(5087, configuredOptions.ApprovalPort);
        Assert.Equal(45, configuredOptions.ApprovalTimeoutSeconds);
        Assert.Equal(
            "ARG_APPROVAL_TOKEN",
            configuredOptions.ApprovalTokenEnvironmentVariable);
    }

    [Fact]
    public void Generated_approval_token_meets_minimum_length()
    {
        var token = ApprovalToken.Generate();

        Assert.True(token.Length >= ApprovalToken.MinimumLength);
        ApprovalToken.Validate(token);
    }

    private static HttpClient CreateAuthorizedClient(
        Uri baseAddress,
        string token)
    {
        var client = new HttpClient
        {
            BaseAddress = baseAddress
        };

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private static async Task WaitForPendingAsync(
        LocalApprovalBroker broker)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (broker.GetPending().Count > 0)
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException(
            "Approval request did not become pending.");
    }

    private static ApprovalRequest Request(Guid decisionId) =>
        new(
            decisionId,
            PolicyEffect.RequireApproval,
            "approval-rule",
            "approval-session",
            "write_file",
            "tools/call",
            "mcp:stdio");
}
