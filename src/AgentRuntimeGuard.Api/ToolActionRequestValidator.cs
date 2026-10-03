using AgentRuntimeGuard.Abstractions;

namespace AgentRuntimeGuard.Api;

internal static class ToolActionRequestValidator
{
    public static Dictionary<string, string[]> Validate(ToolActionRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        AddRequired(errors, nameof(request.AgentId), request.AgentId);
        AddRequired(errors, nameof(request.SessionId), request.SessionId);
        AddRequired(errors, nameof(request.ToolName), request.ToolName);
        AddRequired(errors, nameof(request.Operation), request.Operation);
        AddRequired(errors, nameof(request.Resource), request.Resource);

        return errors;
    }

    private static void AddRequired(
        IDictionary<string, string[]> errors,
        string field,
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required."];
        }
    }
}
