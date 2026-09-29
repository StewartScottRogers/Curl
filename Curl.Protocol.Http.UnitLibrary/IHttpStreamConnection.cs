using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// One HTTP/2 or HTTP/3 request stream presented as the connection an HTTP/1.1 exchange runs
/// on (<see cref="Http2StreamConnection" />, <see cref="Http3StreamConnection" />): the first
/// write is the request head, later writes the body; reads give each response head as curl's
/// own HTTP/2 and HTTP/3 layers write it, then the body, then zero once the stream has ended.
/// </summary>
internal interface IHttpStreamConnection : IConnection
{
    /// <summary>
    /// Gets the response's trailers as header lines with no empty line after them, empty
    /// until the trailing field section has been read.
    /// </summary>
    ReadOnlyMemory<byte> TrailerBytes { get; }

    /// <summary>
    /// Ends the request stream once its body has been written, unless it has ended already or
    /// the response has ended.
    /// </summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the stream's end is sent.</returns>
    ValueTask EndRequestAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reads the rest of the stream, dropping any body bytes past the body, so trailers that
    /// follow a body framed by its Content-Length are read.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the stream has ended.</returns>
    ValueTask ReadToEndAsync(CancellationToken cancellationToken);
}
