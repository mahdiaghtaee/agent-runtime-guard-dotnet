using System.Text.Json;
using System.Text.Json.Serialization;
using AgentRuntimeGuard.Core;

namespace AgentRuntimeGuard.McpProxy;

internal static class PolicyFileLoader
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<PolicyRuleSet> LoadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        await using var stream = File.OpenRead(path);
        var ruleSet = await JsonSerializer.DeserializeAsync<PolicyRuleSet>(
            stream,
            s_options,
            cancellationToken);

        if (ruleSet is null)
        {
            throw new InvalidDataException("The policy file did not contain a rule set.");
        }

        Validate(ruleSet);
        return ruleSet;
    }

    private static void Validate(PolicyRuleSet ruleSet)
    {
        if (string.IsNullOrWhiteSpace(ruleSet.DefaultReason))
        {
            throw new InvalidDataException("defaultReason must not be empty.");
        }

        if (ruleSet.Rules is null)
        {
            throw new InvalidDataException("rules must not be null.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rule in ruleSet.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Id))
            {
                throw new InvalidDataException("Every policy rule requires an id.");
            }

            if (!ids.Add(rule.Id))
            {
                throw new InvalidDataException($"Duplicate policy rule id '{rule.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(rule.ToolPattern)
                || string.IsNullOrWhiteSpace(rule.OperationPattern)
                || string.IsNullOrWhiteSpace(rule.ResourcePattern))
            {
                throw new InvalidDataException(
                    $"Policy rule '{rule.Id}' contains an empty match pattern.");
            }
        }
    }
}
