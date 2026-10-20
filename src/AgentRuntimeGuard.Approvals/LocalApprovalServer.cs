using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentRuntimeGuard.Approvals;

public sealed class LocalApprovalServer : IAsyncDisposable
{
    private readonly WebApplication _application;

    private LocalApprovalServer(
        WebApplication application,
        Uri baseAddress)
    {
        _application = application;
        BaseAddress = baseAddress;
    }

    public Uri BaseAddress { get; }

    public static async Task<LocalApprovalServer> StartAsync(
        LocalApprovalBroker broker,
        string bearerToken,
        int port,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(broker);
        ApprovalToken.Validate(bearerToken);

        if (port is < 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(port),
                "Approval port must be between 0 and 65535.");
        }

        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                Args = []
            });

        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            options.Listen(IPAddress.Loopback, port);
        });

        var app = builder.Build();

        app.Use(
            async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments(
                        "/v1/approvals",
                        StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.Headers.CacheControl = "no-store";
                    context.Response.Headers.Pragma = "no-cache";

                    if (!HasValidBearerToken(
                            context.Request,
                            bearerToken))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.Headers.WWWAuthenticate = "Bearer";
                        return;
                    }
                }

                await next(context);
            });

        app.MapGet(
            "/health",
            () => Results.Ok(
                new
                {
                    service = "agent-runtime-guard-approval",
                    status = "ok"
                }));

        app.MapGet(
            "/v1/approvals",
            () => Results.Ok(broker.GetPending()));

        app.MapPost(
            "/v1/approvals/{decisionId:guid}/approve",
            (Guid decisionId) =>
                broker.TryApprove(decisionId)
                    ? Results.NoContent()
                    : Results.NotFound());

        app.MapPost(
            "/v1/approvals/{decisionId:guid}/reject",
            (Guid decisionId) =>
                broker.TryReject(decisionId)
                    ? Results.NoContent()
                    : Results.NotFound());

        await app.StartAsync(cancellationToken);

        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features
            .Get<IServerAddressesFeature>()
            ?.Addresses
            ?? throw new InvalidOperationException(
                "Kestrel did not publish a bound approval address.");

        var address = addresses
            .Select(value => new Uri(value))
            .Single(uri =>
                IPAddress.TryParse(uri.Host, out var ipAddress)
                && IPAddress.IsLoopback(ipAddress));

        return new LocalApprovalServer(app, address);
    }

    public async ValueTask DisposeAsync()
    {
        await _application.StopAsync();
        await _application.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private static bool HasValidBearerToken(
        HttpRequest request,
        string expectedToken)
    {
        var authorization = request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";

        if (!authorization.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suppliedToken = authorization[prefix.Length..].Trim();
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedToken);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedToken);

        return suppliedBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(
                suppliedBytes,
                expectedBytes);
    }
}
