using System.Text;

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
}
