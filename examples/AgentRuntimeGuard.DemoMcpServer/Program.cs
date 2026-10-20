using System.Text;
using System.Text.Json;

while (await Console.In.ReadLineAsync() is { } line)
{
    if (string.IsNullOrWhiteSpace(line))
    {
        continue;
    }

    string response;

    try
    {
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;

        if (!root.TryGetProperty("method", out var method)
            || method.ValueKind != JsonValueKind.String
            || !string.Equals(
                method.GetString(),
                "tools/call",
                StringComparison.Ordinal))
        {
            response = CreateErrorResponse(
                TryGetId(root),
                -32601,
                "Demo upstream supports tools/call only.");
        }
        else if (!root.TryGetProperty("params", out var parameters)
                 || parameters.ValueKind != JsonValueKind.Object
                 || !parameters.TryGetProperty("name", out var name)
                 || name.ValueKind != JsonValueKind.String
                 || string.IsNullOrWhiteSpace(name.GetString()))
        {
            response = CreateErrorResponse(
                TryGetId(root),
                -32602,
                "tools/call requires params.name.");
        }
        else
        {
            response = CreateToolResult(
                TryGetId(root),
                name.GetString()!);
        }
    }
    catch (JsonException)
    {
        response = CreateErrorResponse(
            id: null,
            -32700,
            "Invalid JSON.");
    }

    await Console.Out.WriteLineAsync(response);
    await Console.Out.FlushAsync();
}

static JsonElement? TryGetId(JsonElement root)
{
    if (!root.TryGetProperty("id", out var id))
    {
        return null;
    }

    return id.ValueKind is JsonValueKind.String
        or JsonValueKind.Number
        or JsonValueKind.Null
        ? id.Clone()
        : null;
}

static string CreateToolResult(
    JsonElement? id,
    string toolName)
{
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream))
    {
        writer.WriteStartObject();
        writer.WriteString("jsonrpc", "2.0");
        WriteId(writer, id);

        writer.WritePropertyName("result");
        writer.WriteStartObject();
        writer.WritePropertyName("content");
        writer.WriteStartArray();
        writer.WriteStartObject();
        writer.WriteString("type", "text");
        writer.WriteString(
            "text",
            $"demo upstream executed: {toolName}");
        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    return Encoding.UTF8.GetString(stream.ToArray());
}

static string CreateErrorResponse(
    JsonElement? id,
    int code,
    string message)
{
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream))
    {
        writer.WriteStartObject();
        writer.WriteString("jsonrpc", "2.0");
        WriteId(writer, id);

        writer.WritePropertyName("error");
        writer.WriteStartObject();
        writer.WriteNumber("code", code);
        writer.WriteString("message", message);
        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    return Encoding.UTF8.GetString(stream.ToArray());
}

static void WriteId(
    Utf8JsonWriter writer,
    JsonElement? id)
{
    writer.WritePropertyName("id");

    if (id is null)
    {
        writer.WriteNullValue();
        return;
    }

    id.Value.WriteTo(writer);
}
