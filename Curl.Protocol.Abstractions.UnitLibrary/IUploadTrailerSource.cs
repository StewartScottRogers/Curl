namespace Curl.Protocol.Abstractions;

/// <summary>
/// An upload stream that supplies the trailer lines of a chunked HTTP/1.1 upload, as libcurl's
/// <c>CURLOPT_TRAILERFUNCTION</c> does. Once the stream's end is read, the HTTP handler asks it
/// once for its <c>Name: value</c> lines and sends each, with its CRLF, between the closing
/// <c>0</c> chunk's size line and the final CRLF, skipping any line with no <c>": "</c> after its
/// first colon, as curl 8.22.0's <c>add_last_chunk</c> in <c>lib/http_chunks.c</c> does. A source
/// that aborts fails the transfer with exit 42 (<see cref="CurlExitCode.AbortedByCallback" />) and
/// <see cref="AbortedMessage" />. A stream that does not implement this, or whose
/// <see cref="SuppliesTrailers" /> is <see langword="false" />, gets the plain <c>0\r\n\r\n</c>.
/// </summary>
public interface IUploadTrailerSource
{
    /// <summary>
    /// libcurl's error text for a trailer callback that returned <c>CURL_TRAILERFUNC_ABORT</c>.
    /// </summary>
    const string AbortedMessage = "operation aborted by trailing headers callback";

    /// <summary>
    /// Gets a value indicating whether the stream has a trailer source at all: curl asks no
    /// callback, and traces the last chunk as empty, when none is set.
    /// </summary>
    bool SuppliesTrailers { get; }

    /// <summary>
    /// Asks the source for the upload's trailer lines, once the body's end is read.
    /// </summary>
    /// <param name="lines">The <c>Name: value</c> lines to send, without their CRLF; empty when the source aborts.</param>
    /// <returns><see langword="true" /> to send <paramref name="lines" />; <see langword="false" /> to abort the transfer.</returns>
    bool TryTakeTrailers(out IReadOnlyList<string> lines);
}
