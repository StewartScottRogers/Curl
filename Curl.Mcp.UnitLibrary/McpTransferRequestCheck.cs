using System.Collections.Frozen;
using System.Text;
using System.Text.Json;

namespace Curl.Mcp;

/// <summary>
/// Checks an <see cref="McpTransferRequest" /> before any transfer starts (ADR-0593): the URL is
/// absolute, has a scheme and is not <c>file:</c>; the method and every header name are HTTP tokens;
/// no header value breaks the line; the timeout is a number of seconds from zero up; a <c>HEAD</c>
/// has no body; and a base64 body decodes. The first field that fails is the one named.
/// </summary>
/// <param name="Failure">The tool error naming the first invalid field; <see langword="null" /> when the request is valid.</param>
/// <param name="Body">The body's bytes when the request is valid and has one; otherwise <see langword="null" />.</param>
public sealed record McpTransferRequestCheck(McpToolFailure? Failure, byte[]? Body)
{
    /// <summary>The longest timeout a request may ask for: 2147483 seconds, about 24.8 days, so that its milliseconds fit the 32-bit <c>long</c> of <c>CURLOPT_TIMEOUT_MS</c> on Windows.</summary>
    public const double MaxTimeoutSeconds = int.MaxValue / 1000;

    private const string TokenSymbols = "!#$%&'*+-.^_`|~";

    /// <summary>Whether the request is valid.</summary>
    public bool IsValid => Failure is null;

    /// <summary>Checks <paramref name="request" />.</summary>
    /// <param name="request">The request to check.</param>
    /// <returns>The check's answer.</returns>
    public static McpTransferRequestCheck Check(McpTransferRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        McpToolFailure? failure = CheckUrl(request.Url)
            ?? CheckMethod(request.Method)
            ?? CheckHeaders(request.Headers)
            ?? CheckTimeout(request.TimeoutSeconds)
            ?? CheckBodyAllowed(request);
        return failure is null ? DecodeBody(request) : new McpTransferRequestCheck(failure, null);
    }

    /// <summary>
    /// Checks the shapes of the <c>transfer</c> tool's raw JSON arguments before they are bound to an
    /// <see cref="McpTransferRequest" /> (ADR-0771), so that a wrong shape, such as <c>headers</c> as an
    /// object or <c>body</c> as a string, is refused naming the field and the shape it must have. An
    /// argument the tool has no field for is left to the binder, which ignores it.
    /// </summary>
    /// <param name="arguments">The tool call's arguments by name; <see langword="null" /> when it has none.</param>
    /// <returns>The tool error naming the first argument of the wrong shape; <see langword="null" /> when every shape is right.</returns>
    public static McpToolFailure? CheckArgumentShapes(IEnumerable<KeyValuePair<string, JsonElement>>? arguments)
    {
        foreach ((string name, JsonElement value) in arguments ?? [])
        {
            if (ArgumentShapes.TryGetValue(name, out McpArgumentShape? shape) && !shape.Fits(value))
            {
                return McpToolFailure.InvalidRequest(name, $"it must be {shape.Description}, not {DescribeKind(value.ValueKind)}.");
            }
        }

        return null;
    }

    /// <summary>The shape each argument of the <c>transfer</c> tool must have, by its name.</summary>
    private static readonly FrozenDictionary<string, McpArgumentShape> ArgumentShapes = new Dictionary<string, McpArgumentShape>
    {
        ["url"] = new("a string", value => IsKindOrNull(value, JsonValueKind.String)),
        ["method"] = new("a string", value => IsKindOrNull(value, JsonValueKind.String)),
        ["headers"] = new("an array of objects with a string name and a string value", IsHeaderArrayOrNull),
        ["body"] = new("an object with a string content and a boolean isBase64", IsBodyOrNull),
        ["followRedirects"] = new("a boolean", IsBoolean),
        ["timeoutSeconds"] = new("a number", value => IsKindOrNull(value, JsonValueKind.Number)),
        ["insecure"] = new("a boolean", IsBoolean),
        ["proxy"] = new("a string", value => IsKindOrNull(value, JsonValueKind.String)),
        ["userAgent"] = new("a string", value => IsKindOrNull(value, JsonValueKind.String)),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static bool IsKindOrNull(JsonElement value, JsonValueKind kind) =>
        value.ValueKind == kind || value.ValueKind == JsonValueKind.Null;

    private static bool IsBoolean(JsonElement value) =>
        value.ValueKind is JsonValueKind.True or JsonValueKind.False;

    private static bool IsHeaderArrayOrNull(JsonElement value) =>
        value.ValueKind == JsonValueKind.Null
        || (value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(IsHeader));

    private static bool IsHeader(JsonElement header) =>
        header.ValueKind == JsonValueKind.Object
        && HasPropertyOfShape(header, "name", JsonValueKind.String)
        && HasPropertyOfShape(header, "value", JsonValueKind.String);

    /// <summary>A body needs its <c>content</c>, as it is the text sent; <c>isBase64</c> may be left out, and then is false.</summary>
    private static bool IsBodyOrNull(JsonElement value) =>
        value.ValueKind == JsonValueKind.Null
        || (value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty("content", out JsonElement content)
            && content.ValueKind == JsonValueKind.String
            && (!value.TryGetProperty("isBase64", out JsonElement isBase64) || IsBoolean(isBase64)));

    /// <summary>Whether <paramref name="element" />'s <paramref name="name" /> property is there and of <paramref name="kind" />.</summary>
    private static bool HasPropertyOfShape(JsonElement element, string name, JsonValueKind kind) =>
        element.TryGetProperty(name, out JsonElement property) && property.ValueKind == kind;

    private static string DescribeKind(JsonValueKind kind) => kind switch
    {
        JsonValueKind.Object => "an object",
        JsonValueKind.Array => "an array",
        JsonValueKind.String => "a string",
        JsonValueKind.Number => "a number",
        JsonValueKind.True or JsonValueKind.False => "a boolean",
        _ => "null",
    };

    /// <summary>The "://" test keeps <see cref="Uri" /> from reading a bare path such as <c>/tmp/x</c> as an absolute <c>file</c> URI.</summary>
    private static McpToolFailure? CheckUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return McpToolFailure.InvalidRequest("url", "it is empty.");
        }

        if (!url.Contains("://", StringComparison.Ordinal) || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return McpToolFailure.InvalidRequest("url", $"'{url}' is not an absolute URL with a scheme.");
        }

        return uri.IsFile
            ? McpToolFailure.InvalidRequest("url", "file: URLs reach local files, which the transfer tool does not offer; use the curl tool, which confines them to CURL_MCP_ROOT.")
            : null;
    }

    private static McpToolFailure? CheckMethod(string? method) =>
        IsToken(method) ? null : McpToolFailure.InvalidRequest("method", $"'{method}' is not an HTTP method token.");

    private static McpToolFailure? CheckHeaders(IReadOnlyList<McpTransferHeader> headers)
    {
        for (int index = 0; index < headers.Count; index++)
        {
            McpToolFailure? failure = CheckHeader(headers[index], index);
            if (failure is not null)
            {
                return failure;
            }
        }

        return null;
    }

    private static McpToolFailure? CheckHeader(McpTransferHeader header, int index)
    {
        if (!IsToken(header.Name))
        {
            return McpToolFailure.InvalidRequest($"headers[{index}].name", $"'{header.Name}' is not a header name.");
        }

        return header.Value.AsSpan().ContainsAny('\r', '\n')
            ? McpToolFailure.InvalidRequest($"headers[{index}].value", "it holds a line break.")
            : null;
    }

    private static McpToolFailure? CheckTimeout(double? timeoutSeconds) =>
        timeoutSeconds is not double seconds || seconds is >= 0 and <= MaxTimeoutSeconds
            ? null
            : McpToolFailure.InvalidRequest("timeoutSeconds", $"it must be from 0 to {MaxTimeoutSeconds} seconds.");

    private static McpToolFailure? CheckBodyAllowed(McpTransferRequest request) =>
        request.Body is not null && request.Method == "HEAD"
            ? McpToolFailure.InvalidRequest("body", "a HEAD request carries no body.")
            : null;

    /// <summary>The valid-request answer: the body as UTF-8 text bytes, as decoded base64, or none; invalid base64 is a failure.</summary>
    private static McpTransferRequestCheck DecodeBody(McpTransferRequest request)
    {
        McpTransferBody? body = request.Body;
        if (body is null)
        {
            return new McpTransferRequestCheck(null, null);
        }

        if (!body.IsBase64)
        {
            return new McpTransferRequestCheck(null, Encoding.UTF8.GetBytes(body.Content));
        }

        try
        {
            return new McpTransferRequestCheck(null, Convert.FromBase64String(body.Content));
        }
        catch (FormatException)
        {
            return new McpTransferRequestCheck(McpToolFailure.InvalidRequest("body", "it is not valid base64."), null);
        }
    }

    /// <summary>Whether <paramref name="text" /> is an RFC 9110 token: one or more letters, digits and token symbols.</summary>
    private static bool IsToken(string? text) =>
        !string.IsNullOrEmpty(text) && text.All(character => char.IsAsciiLetterOrDigit(character) || TokenSymbols.Contains(character, StringComparison.Ordinal));

    /// <summary>The shape one argument must have: its description, as the tool error names it, and its test.</summary>
    /// <param name="Description">The shape, e.g. <c>a string</c>.</param>
    /// <param name="Fits">Whether a JSON value has the shape.</param>
    private sealed record McpArgumentShape(string Description, Func<JsonElement, bool> Fits);
}
