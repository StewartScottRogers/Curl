using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Ends an HTTP transfer early with the curl exit code and message the transfer reports.
/// Thrown while a response is read and caught by the handler that turns it into a
/// <see cref="TransferResult" />; it never leaves this library.
/// </summary>
/// <param name="exitCode">The curl exit code the transfer reports.</param>
/// <param name="message">The message curl prints for the failure.</param>
internal sealed class HttpTransferException(CurlExitCode exitCode, string message)
    : Exception(message)
{
    /// <summary>
    /// Gets the curl exit code the transfer reports.
    /// </summary>
    internal CurlExitCode ExitCode { get; } = exitCode;

    /// <summary>
    /// Gets a value indicating whether the server refused the request's HTTP/3 stream with
    /// <c>H3_REQUEST_REJECTED</c>, so the handler may send it again on a new connection
    /// (ADR-0187). The message is then the <c>-v</c> line that reports the refusal.
    /// </summary>
    internal bool IsStreamRefused { get; init; }

    /// <summary>
    /// Gets the <c>-v</c> lines the handler reports for the failure, in order, once the head
    /// lines it holds are reported; none by default. Too many response headers reports its
    /// message, and among a chunked body's trailers also
    /// <see cref="HttpTransferMessages.ChunkedStreamReadFailed" /> after it (measured, BL-1448 Notes).
    /// </summary>
    internal IReadOnlyList<string> InfoLines { get; init; } = [];

    /// <summary>
    /// Gets the head bytes accepted before the failure, which the handler writes to the header
    /// output before it fails, as libcurl 8.22.0 writes each header line once it accepts it
    /// (upstream test580, BL-2587); none by default. The head reader sets it on a failure among
    /// the header lines or after a 1xx head (upstream tests 158 and 415, BL-2867).
    /// </summary>
    internal byte[] HeadBytesBeforeFailure { get; set; } = [];

    /// <summary>
    /// Gets or sets the status line of the head the failure cut short, or <see langword="null" /> when it came
    /// before one: libcurl 8.22.0 still reports that response's code and version, and the bytes of
    /// <see cref="HeadBytesBeforeFailure" /> as its header size, after a failure among its header lines (BL-3195).
    /// </summary>
    internal HttpStatusLine? StatusLineBeforeFailure { get; set; }
}
