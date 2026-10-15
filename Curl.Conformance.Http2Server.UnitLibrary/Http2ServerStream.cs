using Curl.Http2;

namespace Curl.Conformance.Http2Server;

/// <summary>
/// One client stream on the h2c server: its request fields, the body received so far, and how far
/// the server's DATA has moved its send window away from the client's initial window size.
/// </summary>
/// <param name="Request">The request's HTTP/2 header fields.</param>
internal sealed record Http2ServerStream(IReadOnlyList<HeaderField> Request)
{
    /// <summary>
    /// Gets the request body received so far.
    /// </summary>
    internal MemoryStream Body { get; } = new();

    /// <summary>
    /// Gets or sets the client's WINDOW_UPDATE increments less the DATA bytes sent, so the stream's
    /// send window is the client's SETTINGS_INITIAL_WINDOW_SIZE plus this.
    /// </summary>
    internal int SendWindowAdjustment { get; set; }
}
