namespace Curl.Mcp;

/// <summary>
/// What the <c>transfer</c> tool gives back (ADR-0548, ADR-0593): the response's status, headers,
/// body and transfer information when the transfer succeeded, and only a tool error when the request
/// was invalid, the transfer failed or the request was cancelled.
/// </summary>
/// <param name="StatusCode">The last response's status code; <see langword="null" /> on failure.</param>
/// <param name="Headers">The last response's headers in the order received, repeats kept; <see langword="null" /> on failure.</param>
/// <param name="Body">The response body, text or base64, cut to the cap with the cut stated; <see langword="null" /> on failure.</param>
/// <param name="Info">What the transfer reported; <see langword="null" /> on failure.</param>
/// <param name="Failure">The tool error; <see langword="null" /> when the transfer succeeded.</param>
public sealed record McpTransferToolResult(
    long? StatusCode,
    IReadOnlyList<McpTransferHeader>? Headers,
    McpReturnedBytes? Body,
    McpTransferInfo? Info,
    McpToolFailure? Failure)
{
    /// <summary>Whether the result is a tool error.</summary>
    public bool IsError => Failure is not null;

    /// <summary>Whether the body was cut at the cap.</summary>
    public bool IsBodyTruncated => Body?.IsTruncated ?? false;

    /// <summary>The result for a transfer that failed or never ran.</summary>
    /// <param name="failure">Why.</param>
    /// <returns>A result holding only the tool error.</returns>
    public static McpTransferToolResult Failed(McpToolFailure failure) => new(null, null, null, null, failure);
}
