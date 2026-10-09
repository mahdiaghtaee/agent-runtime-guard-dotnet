using AgentRuntimeGuard.McpProxy;

return await McpProxyApplication.RunAsync(
    args,
    Console.In,
    Console.Out,
    Console.Error,
    CancellationToken.None);
