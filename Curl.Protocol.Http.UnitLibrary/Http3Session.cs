using System.Net;
using Curl.Http3;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// One QUIC connection speaking HTTP/3 for the HTTP handler: the QPACK contexts every request
/// stream on it shares, and the client's control and QPACK encoder and decoder streams, which
/// open with the first request, before its HEADERS, carrying curl's <c>SETTINGS</c>
/// (<see cref="Http3LocalUnidirectionalStreams.CurlSettings" />). QPACK runs with the dynamic
/// table capacity and blocked streams curl advertises, 0, so requests are encoded with the
/// static table and literals only (ADR-0144 section 5).
/// </summary>
/// <remarks>
/// The session is the <see cref="IConnection" /> the handler holds for the transfer, so its
/// connect result, timings and disposal follow the same path as a TCP connection's; bytes
/// travel only on its streams, so reading or writing the session itself throws
/// <see cref="NotSupportedException" />. Disposing it disposes every stream it opened, closes the
/// connection with <c>H3_NO_ERROR</c> and disposes the connection.
/// </remarks>
/// <param name="connection">The QUIC connection, past its handshake; the session owns it.</param>
internal sealed class Http3Session(IMultiplexedConnection connection) : IHttpStreamSession, IConnection
{
    private readonly List<IMultiplexedStream> openedStreams = [];

    /// <summary>Gets the QUIC connection the session runs on.</summary>
    internal IMultiplexedConnection Connection { get; } = connection;

    /// <summary>Gets the encoder every request field section passes through.</summary>
    internal QpackEncoder Encoder { get; } = new(maximumTableCapacity: 0, maximumBlockedStreams: 0);

    /// <summary>Gets the decoder every response field section passes through.</summary>
    internal QpackDecoder Decoder { get; } = new(maximumTableCapacity: 0, maximumBlockedStreams: 0);

    /// <inheritdoc />
    public string VersionName => "HTTP/3";

    /// <inheritdoc />
    public string UsingLine => HttpConnectionInfoLines.UsingHttp3;

    /// <summary>
    /// Gets a value indicating whether the connection can carry another request: always, since
    /// the server's control stream, whose <c>GOAWAY</c> would end that, is not read yet.
    /// </summary>
    public bool AcceptsNewStreams => true;

    /// <summary>Gets a value indicating that QUIC traffic is always encrypted.</summary>
    public bool IsSecure => true;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => Connection.RemoteEndPoint;

    /// <inheritdoc />
    public EndPoint? LocalEndPoint => Connection.LocalEndPoint;

    /// <inheritdoc />
    public IHttpStreamConnection CreateStream(string scheme, long? bodyLength) => new Http3StreamConnection(this, scheme, bodyLength);

    /// <summary>
    /// Opens the client's control stream with curl's <c>SETTINGS</c> and its QPACK encoder
    /// and decoder streams, unless they are open already, then opens a request stream.
    /// </summary>
    /// <param name="cancellationToken">Cancels the opens and writes.</param>
    /// <returns>The request stream.</returns>
    internal async ValueTask<IMultiplexedStream> OpenRequestStreamAsync(CancellationToken cancellationToken)
    {
        if (openedStreams.Count == 0) // the first request: the three unidirectional streams go first
        {
            await Http3LocalUnidirectionalStreams.OpenControlStreamAsync(await OpenLocalStreamAsync(cancellationToken).ConfigureAwait(false), Http3LocalUnidirectionalStreams.CurlSettings, cancellationToken).ConfigureAwait(false);
            await Http3LocalUnidirectionalStreams.OpenQpackEncoderStreamAsync(await OpenLocalStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
            await Http3LocalUnidirectionalStreams.OpenQpackDecoderStreamAsync(await OpenLocalStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        }

        IMultiplexedStream requestStream = await Connection.OpenBidirectionalStreamAsync(cancellationToken).ConfigureAwait(false);
        openedStreams.Add(requestStream);
        return requestStream;
    }

    /// <summary>Always throws: bytes travel only on the session's streams.</summary>
    /// <param name="buffer">Not used.</param>
    /// <param name="cancellationToken">Not used.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Always throws: bytes travel only on the session's streams.</summary>
    /// <param name="buffer">Not used.</param>
    /// <param name="cancellationToken">Not used.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Does nothing: each stream sends what it is given.</summary>
    /// <param name="cancellationToken">Not used.</param>
    /// <returns>A completed task.</returns>
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <summary>
    /// Disposes every stream it opened, closes the connection with <c>H3_NO_ERROR</c> - unless it
    /// is lost already - and disposes it.
    /// </summary>
    /// <returns>A task that completes when the connection is disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        foreach (IMultiplexedStream stream in openedStreams)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }

        try
        {
            await Connection.CloseAsync((long)Http3ErrorCode.NoError, CancellationToken.None).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The connection is lost already; there is nobody to tell.
        }

        await Connection.DisposeAsync().ConfigureAwait(false);
    }

    private async ValueTask<Stream> OpenLocalStreamAsync(CancellationToken cancellationToken)
    {
        IMultiplexedStream stream = await Connection.OpenUnidirectionalStreamAsync(cancellationToken).ConfigureAwait(false);
        openedStreams.Add(stream);
        return new MultiplexedStreamAdapter(stream);
    }
}
