using System.Text.Json.Serialization;
using AgentRuntimeGuard.Abstractions;
using AgentRuntimeGuard.Api;
using AgentRuntimeGuard.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSingleton<IPolicyEvaluator>(_ =>
{
    var settings = builder.Configuration
        .GetSection("Policy")
        .Get<PolicySettings>()
        ?? new PolicySettings();

    return new PolicyEvaluator(settings.ToRuleSet());
});

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    service = "agent-runtime-guard",
    status = "ok"
}));

app.MapPost(
    "/v1/evaluate",
    async (
        ToolActionRequest request,
        IPolicyEvaluator evaluator,
        CancellationToken cancellationToken) =>
    {
        var errors = ToolActionRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var result = await evaluator.EvaluateAsync(request, cancellationToken);
        return Results.Ok(result);
    });

app.Run();

public partial class Program
{
}
