using System.Text.Json;

namespace Curl.Extended.Protocol.AzureStorage.Bodies;

/// <summary>
/// Reads the JSON error body the ADLS Gen2 (DFS) REST API returns with a failed request,
/// <c>{"error":{"code":"PathNotFound","message":"The specified path does not exist."}}</c>: its
/// <c>error.code</c> and <c>error.message</c>.
/// </summary>
public static class AzureStorageJsonErrorParser
{
    /// <summary>Reads <paramref name="json"/> as the error the DFS endpoint returned.</summary>
    /// <param name="json">The response body.</param>
    /// <returns>The error, or why the body could not be read.</returns>
    public static AzureStorageBodyParseResult<AzureStorageError> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            return AzureStorageBodyParseResult<AzureStorageError>.Success(ReadError(document.RootElement));
        }
        catch (Exception exception) when (exception is JsonException or AzureStorageBodyException)
        {
            return AzureStorageBodyParseResult<AzureStorageError>.Malformed(exception.Message);
        }
    }

    private static AzureStorageError ReadError(JsonElement root)
    {
        RequireKind(root, JsonValueKind.Object, "The body");
        if (!root.TryGetProperty("error", out var error))
        {
            throw new AzureStorageBodyException("The body has no error.");
        }

        RequireKind(error, JsonValueKind.Object, "error");
        var code = OptionalString(error, "code") ?? throw new AzureStorageBodyException("error has no code.");
        return new(code, OptionalString(error, "message") ?? string.Empty);
    }

    private static string? OptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        RequireKind(value, JsonValueKind.String, propertyName);
        return value.GetString();
    }

    private static void RequireKind(JsonElement element, JsonValueKind kind, string what)
    {
        if (element.ValueKind != kind)
        {
            throw new AzureStorageBodyException($"{what} is a JSON {element.ValueKind}, not {kind}.");
        }
    }
}
