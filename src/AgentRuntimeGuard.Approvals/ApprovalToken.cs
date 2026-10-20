using System.Security.Cryptography;

namespace AgentRuntimeGuard.Approvals;

public static class ApprovalToken
{
    public const int MinimumLength = 32;

    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);

        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static void Validate(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        if (token.Length < MinimumLength)
        {
            throw new ArgumentException(
                $"Approval bearer token must contain at least {MinimumLength} characters.",
                nameof(token));
        }
    }
}
