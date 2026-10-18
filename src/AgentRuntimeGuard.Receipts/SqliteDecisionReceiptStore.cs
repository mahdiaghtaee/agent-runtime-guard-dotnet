using System.Globalization;
using AgentRuntimeGuard.Abstractions;
using Microsoft.Data.Sqlite;

namespace AgentRuntimeGuard.Receipts;

public sealed class SqliteDecisionReceiptStore : IDecisionReceiptStore
{
    public const int DefaultMaxReceipts = 10_000;
    public const int MaxQueryLimit = 1_000;

    private readonly string _connectionString;
    private readonly int _maxReceipts;

    public SqliteDecisionReceiptStore(
        string databasePath,
        int maxReceipts = DefaultMaxReceipts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        if (maxReceipts < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxReceipts),
                "Retention must keep at least one receipt.");
        }

        var fullPath = Path.GetFullPath(databasePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        _maxReceipts = maxReceipts;
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(
            cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS decision_receipts
            (
                sequence INTEGER PRIMARY KEY AUTOINCREMENT,
                decision_id TEXT NOT NULL UNIQUE,
                evaluated_at_utc TEXT NOT NULL,
                effect TEXT NOT NULL,
                rule_id TEXT NULL,
                agent_id TEXT NOT NULL,
                session_id TEXT NOT NULL,
                tool_name TEXT NOT NULL,
                operation TEXT NOT NULL,
                resource TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_decision_receipts_session_sequence
                ON decision_receipts(session_id, sequence DESC);
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AppendAsync(
        DecisionReceipt receipt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        await using var connection = await OpenConnectionAsync(
            cancellationToken);
        await using var transaction = connection.BeginTransaction();

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO decision_receipts
                (
                    decision_id,
                    evaluated_at_utc,
                    effect,
                    rule_id,
                    agent_id,
                    session_id,
                    tool_name,
                    operation,
                    resource
                )
                VALUES
                (
                    $decision_id,
                    $evaluated_at_utc,
                    $effect,
                    $rule_id,
                    $agent_id,
                    $session_id,
                    $tool_name,
                    $operation,
                    $resource
                );
                """;

            insert.Parameters.AddWithValue(
                "$decision_id",
                receipt.DecisionId.ToString("D"));
            insert.Parameters.AddWithValue(
                "$evaluated_at_utc",
                receipt.EvaluatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            insert.Parameters.AddWithValue(
                "$effect",
                receipt.Effect.ToString());
            insert.Parameters.AddWithValue(
                "$rule_id",
                (object?)receipt.RuleId ?? DBNull.Value);
            insert.Parameters.AddWithValue("$agent_id", receipt.AgentId);
            insert.Parameters.AddWithValue("$session_id", receipt.SessionId);
            insert.Parameters.AddWithValue("$tool_name", receipt.ToolName);
            insert.Parameters.AddWithValue("$operation", receipt.Operation);
            insert.Parameters.AddWithValue("$resource", receipt.Resource);

            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var prune = connection.CreateCommand())
        {
            prune.Transaction = transaction;
            prune.CommandText =
                """
                DELETE FROM decision_receipts
                WHERE sequence IN
                (
                    SELECT sequence
                    FROM decision_receipts
                    ORDER BY sequence DESC
                    LIMIT -1 OFFSET $max_receipts
                );
                """;
            prune.Parameters.AddWithValue("$max_receipts", _maxReceipts);

            await prune.ExecuteNonQueryAsync(cancellationToken);
        }

        transaction.Commit();
    }

    public async Task<IReadOnlyList<DecisionReceipt>> QueryBySessionAsync(
        string sessionId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        if (limit is < 1 or > MaxQueryLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                $"Query limit must be between 1 and {MaxQueryLimit}.");
        }

        await using var connection = await OpenConnectionAsync(
            cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                sequence,
                decision_id,
                evaluated_at_utc,
                effect,
                rule_id,
                agent_id,
                session_id,
                tool_name,
                operation,
                resource
            FROM decision_receipts
            WHERE session_id = $session_id
            ORDER BY sequence DESC
            LIMIT $limit;
            """;

        command.Parameters.AddWithValue(
            "$session_id",
            ClipSessionId(sessionId));
        command.Parameters.AddWithValue("$limit", limit);

        var receipts = new List<DecisionReceipt>();

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            receipts.Add(
                new DecisionReceipt(
                    Sequence: reader.GetInt64(0),
                    DecisionId: Guid.Parse(reader.GetString(1)),
                    EvaluatedAtUtc: DateTimeOffset.Parse(
                        reader.GetString(2),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind),
                    Effect: Enum.Parse<PolicyEffect>(
                        reader.GetString(3),
                        ignoreCase: false),
                    RuleId: reader.IsDBNull(4) ? null : reader.GetString(4),
                    AgentId: reader.GetString(5),
                    SessionId: reader.GetString(6),
                    ToolName: reader.GetString(7),
                    Operation: reader.GetString(8),
                    Resource: reader.GetString(9)));
        }

        return receipts;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static string ClipSessionId(string sessionId) =>
        sessionId.Length <= DecisionReceipt.SessionIdMaxLength
            ? sessionId
            : sessionId[..DecisionReceipt.SessionIdMaxLength];
}
