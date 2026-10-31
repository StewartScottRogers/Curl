using Curl.Http2;

namespace Curl.Extension.WebSocketExtendedConnect.Wire;

/// <summary>
/// Validates the response to an extended CONNECT: any 2xx <c>:status</c> opens the WebSocket (RFC 8441 section 5,
/// RFC 9220 section 3, RFC 9113 section 8.5); anything else, or no <c>:status</c>, refuses it.
/// </summary>
internal static class ExtendedConnectResponse
{
    /// <summary>Gives the response's <c>:status</c> value.</summary>
    /// <param name="fields">The response's fields.</param>
    /// <returns>The status, or <see langword="null" /> when the response carries none.</returns>
    internal static string? StatusOf(IReadOnlyList<HeaderField> fields) =>
        fields.FirstOrDefault(field => field.Name == ":status").Value;

    /// <summary>Gives whether the response accepts the extended CONNECT.</summary>
    /// <param name="fields">The response's fields.</param>
    /// <returns><see langword="true" /> for a three-digit 2xx <c>:status</c>.</returns>
    internal static bool IsAccepted(IReadOnlyList<HeaderField> fields) =>
        StatusOf(fields) is { Length: 3 } status && status[0] == '2' && char.IsAsciiDigit(status[1]) && char.IsAsciiDigit(status[2]);
}
