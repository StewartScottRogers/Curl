using Curl.Http2;

namespace Curl.Extended.Protocol.Grpc.Client;

/// <summary>
/// One HTTP/2 connection a transfer's gRPC streams share, the server reflection stream and then the call (ADR-1149), with the
/// HPACK encoder and decoder whose dynamic tables span every stream on it.
/// </summary>
/// <param name="Connection">The connection, its preface sent.</param>
/// <param name="Encoder">Encodes the header blocks the client sends.</param>
/// <param name="Decoder">Decodes the header blocks the server sends.</param>
internal sealed record GrpcHttp2Session(Http2Connection Connection, HpackEncoder Encoder, HpackDecoder Decoder)
{
    /// <summary>Starts HTTP/2 on a transport by sending the client preface.</summary>
    /// <param name="transport">The byte stream the connection runs on, past any TLS handshake; the caller owns it.</param>
    /// <param name="cancellationToken">Cancels the preface.</param>
    /// <returns>The session.</returns>
    public static async Task<GrpcHttp2Session> OpenAsync(Stream transport, CancellationToken cancellationToken)
    {
        var connection = new Http2Connection(transport);
        await connection.SendPrefaceAsync(cancellationToken).ConfigureAwait(false);
        return new GrpcHttp2Session(connection, new HpackEncoder(), new HpackDecoder());
    }

    /// <summary>Opens a stream and sends its request headers, leaving the stream open for its messages.</summary>
    /// <param name="headers">The Request-Headers, names lower case, pseudo-headers first.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The stream's identifier.</returns>
    public async Task<int> OpenStreamAsync(IReadOnlyList<KeyValuePair<string, string>> headers, CancellationToken cancellationToken)
    {
        int streamId = Connection.OpenStream();
        byte[] block = Encoder.Encode(headers.Select(static header => new HeaderField(header.Key, header.Value)));
        await Connection.WriteHeadersAsync(streamId, block, isEndStream: false, cancellationToken).ConfigureAwait(false);
        return streamId;
    }
}
