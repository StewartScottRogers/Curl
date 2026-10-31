using Curl.Extended.Protocol.Grpc.Client;
using Curl.Extended.Protocol.Grpc.Wire;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Grpc.Reflection;

/// <summary>
/// The bidirectional <c>grpc.reflection.v1.ServerReflection/ServerReflectionInfo</c> stream (ADR-1149): sends one
/// <c>ServerReflectionRequest</c> at a time and reads the <c>ServerReflectionResponse</c> the server answers it with, then
/// ends its half of the stream and reads the server's to its trailers, so the call that follows has the connection alone.
/// </summary>
internal sealed class ServerReflectionStream
{
    /// <summary>The reflection service's fully qualified name.</summary>
    public const string Service = "grpc.reflection.v1.ServerReflection";

    /// <summary>The reflection method's name.</summary>
    public const string Method = "ServerReflectionInfo";

    private readonly GrpcHttp2Session session;
    private readonly int streamId;
    private readonly GrpcHttp2Call.ResponseReader response;
    private int answered;

    private ServerReflectionStream(GrpcHttp2Session session, int streamId)
    {
        this.session = session;
        this.streamId = streamId;
        response = new GrpcHttp2Call.ResponseReader(session.Decoder);
    }

    /// <summary>Opens the stream on the call's connection.</summary>
    /// <param name="session">The connection.</param>
    /// <param name="callTarget">The call the reflection is for: its scheme and authority are the stream's.</param>
    /// <param name="timeout">The <c>grpc-timeout</c> to send, or <see langword="null" /> for none.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The open stream.</returns>
    public static async Task<ServerReflectionStream> OpenAsync(GrpcHttp2Session session, GrpcRequestTarget callTarget, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        var headers = GrpcRequestHeaders.Build(callTarget with { Service = Service, Method = Method }, timeout, null);
        return new ServerReflectionStream(session, await session.OpenStreamAsync(headers, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Sends a request and reads the response to it.</summary>
    /// <param name="request">The <c>ServerReflectionRequest</c>'s binary form.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The response.</returns>
    /// <exception cref="GrpcClientException">
    /// The server ended the stream with a status instead of answering (exit 22), its flow-control window did not take the
    /// request, or it closed the connection (exit 16).
    /// </exception>
    /// <exception cref="GrpcWireException">The response breaks PROTOCOL-HTTP2.md or does not decode.</exception>
    public async Task<ServerReflectionResponse> ExchangeAsync(byte[] request, CancellationToken cancellationToken)
    {
        byte[] framed = LengthPrefixedMessage.Frame(request, isCompressed: false);
        int written = await session.Connection.WriteDataAsync(streamId, framed, isEndStream: false, cancellationToken).ConfigureAwait(false);
        if (written < framed.Length)
        {
            throw new GrpcClientException(CurlExitCode.Http2, "The server's HTTP/2 flow-control window did not take a server reflection request");
        }

        while (response.Messages.Count == answered)
        {
            if (response.Read(await ReadFrameAsync(cancellationToken).ConfigureAwait(false)) is { } status)
            {
                throw new GrpcClientException(
                    CurlExitCode.HttpReturnedError,
                    $"The server did not answer gRPC server reflection (grpc-status {(int)status.Code} ({status.CodeName}): {status.Message}); give the method's .proto file with --grpc-proto");
            }
        }

        return ServerReflectionResponse.Decode(response.Messages[answered++].Payload);
    }

    /// <summary>Ends the client's half of the stream and reads the server's to its trailers, whatever status they carry.</summary>
    /// <param name="cancellationToken">Cancels the close.</param>
    /// <returns>A task that completes when the server has ended the stream.</returns>
    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        await session.Connection.WriteDataAsync(streamId, ReadOnlyMemory<byte>.Empty, isEndStream: true, cancellationToken).ConfigureAwait(false);
        while (response.Read(await ReadFrameAsync(cancellationToken).ConfigureAwait(false)) is null)
        {
        }
    }

    private async Task<Http2StreamFrame> ReadFrameAsync(CancellationToken cancellationToken) =>
        await session.Connection.ReadStreamFrameAsync(cancellationToken).ConfigureAwait(false)
        ?? throw new GrpcClientException(CurlExitCode.Http2, "The server closed the HTTP/2 connection during gRPC server reflection");
}
