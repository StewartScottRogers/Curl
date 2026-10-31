using System.Globalization;
using System.Text.Json;

namespace Curl.Extension.Protocol.AzureStorage.Bodies;

/// <summary>
/// Reads the JSON body of the ADLS Gen2 REST API "Path - List": its <c>paths</c> array, each entry's
/// <c>name</c>, <c>isDirectory</c>, <c>contentLength</c>, <c>lastModified</c> and <c>etag</c>. The
/// service sends <c>isDirectory</c> and <c>contentLength</c> as strings (<c>"true"</c>, <c>"0"</c>);
/// JSON booleans and numbers read alike.
/// </summary>
public static class AzureStoragePathListParser
{
    /// <summary>Reads <paramref name="json"/> as one Path List page.</summary>
    /// <param name="json">The response body.</param>
    /// <returns>The page, or why the body could not be read.</returns>
    public static AzureStorageBodyParseResult<AzureStoragePathList> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            return AzureStorageBodyParseResult<AzureStoragePathList>.Success(ReadPathList(document.RootElement));
        }
        catch (Exception exception) when (exception is JsonException or FormatException or OverflowException or AzureStorageBodyException)
        {
            return AzureStorageBodyParseResult<AzureStoragePathList>.Malformed(exception.Message);
        }
    }

    private static AzureStoragePathList ReadPathList(JsonElement root)
    {
        RequireKind(root, JsonValueKind.Object, "The body");
        if (!root.TryGetProperty("paths", out var paths))
        {
            throw new AzureStorageBodyException("The body has no paths.");
        }

        RequireKind(paths, JsonValueKind.Array, "paths");
        return new([.. paths.EnumerateArray().Select(ReadPath)]);
    }

    private static AzureStoragePath ReadPath(JsonElement path)
    {
        RequireKind(path, JsonValueKind.Object, "A paths entry");
        var name = OptionalString(path, "name") ?? throw new AzureStorageBodyException("A paths entry has no name.");
        var lastModified = OptionalString(path, "lastModified");
        return new(
            name,
            ReadIsDirectory(path),
            ReadContentLength(path),
            lastModified is null ? null : AzureStorageHttpTime.Parse(lastModified),
            OptionalString(path, "etag"));
    }

    private static bool ReadIsDirectory(JsonElement path)
    {
        if (!path.TryGetProperty("isDirectory", out var value))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return bool.Parse(value.GetString()!);
        }

        return value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : throw new AzureStorageBodyException("isDirectory is neither a boolean nor a string.");
    }

    private static long ReadContentLength(JsonElement path)
    {
        if (!path.TryGetProperty("contentLength", out var value))
        {
            return 0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt64(out var number) && number >= 0
                ? number
                : throw new AzureStorageBodyException("contentLength is not a whole number of bytes."),
            JsonValueKind.String => long.Parse(value.GetString()!, NumberStyles.None, CultureInfo.InvariantCulture),
            _ => throw new AzureStorageBodyException("contentLength is neither a number nor a string."),
        };
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
