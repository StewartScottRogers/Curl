using System.Net;
using System.Net.Security;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// One connection to a stand-in behind implicit TLS, as upstream's stunnel fronts a plain server:
/// the client reads and writes TLS records, and behind a <see cref="TlsServerStream"/> the
/// decrypted bytes are relayed both ways at once to a plain server connection, so a server that
/// speaks first, as a mail server greets, is heard. Once the plain server closes, the TLS
/// connection is ended with close_notify, as stunnel does.
/// </summary>
internal sealed class TlsRelayConnection : IConnection
{
    private const int RelayBufferSize = 16384;

    private readonly InMemoryDuplexStream clientEnd;

    private readonly Task serving;

    public TlsRelayConnection(IConnection server, TlsServerOptions options, EndPoint remoteEndPoint)
    {
        (clientEnd, InMemoryDuplexStream serverEnd) = InMemoryDuplexStream.CreatePair();
        RemoteEndPoint = remoteEndPoint;
        LocalEndPoint = server.LocalEndPoint;
        serving = ServeAsync(server, serverEnd, options);
    }

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint { get; }

    public EndPoint? LocalEndPoint { get; }

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        clientEnd.ReadAsync(buffer, cancellationToken);

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        clientEnd.WriteAsync(buffer, cancellationToken);

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await clientEnd.DisposeAsync().ConfigureAwait(false);
        await serving.ConfigureAwait(false);
    }

    // Whatever ends the exchange - a failed handshake, the client closing or vanishing - ends this
    // connection as a closed socket would; the client sees the end of its stream. Once the client
    // has closed, the relay from a server still waiting for a command is cancelled.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Any failure ends the connection, as stunnel drops it.")]
    private static async Task ServeAsync(IConnection server, InMemoryDuplexStream serverEnd, TlsServerOptions options)
    {
        await Task.Yield();
        using CancellationTokenSource clientClosed = new();
        try
        {
            await using SslStream tls = await TlsServerStream.AuthenticateAsync(serverEnd, options, CancellationToken.None).ConfigureAwait(false);
            Task fromServer = RelayFromServerAsync(server, tls, clientClosed.Token);
            await RelayFromClientAsync(tls, server).ConfigureAwait(false);
            await clientClosed.CancelAsync().ConfigureAwait(false);
            await fromServer.ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
        finally
        {
            await clientClosed.CancelAsync().ConfigureAwait(false);
            await serverEnd.DisposeAsync().ConfigureAwait(false);
            await server.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task RelayFromClientAsync(SslStream tls, IConnection server)
    {
        byte[] buffer = new byte[RelayBufferSize];
        int read;
        while ((read = await tls.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            await server.WriteAsync(buffer.AsMemory(0, read), CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static async Task RelayFromServerAsync(IConnection server, SslStream tls, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[RelayBufferSize];
        int read;
        while ((read = await server.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await tls.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        // stunnel sends close_notify when the server closes, so curl sees a clean end of the stream.
        await tls.ShutdownAsync().ConfigureAwait(false);
    }
}
