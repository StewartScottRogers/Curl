using System.Net;
using System.Net.Security;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// One connection to the HTTPS stand-in: the client reads and writes TLS records, and behind a
/// <see cref="TlsServerStream"/> the decrypted requests reach an sws emulation connection, as
/// upstream's stunnel hands them to sws. The server takes turns: it reads request bytes, hands
/// them to sws, then relays every reply byte sws has until it has none, and ends the TLS
/// connection once sws no longer reads requests on it.
/// </summary>
internal sealed class TlsServerConnection : IConnection
{
    private const int RelayBufferSize = 16384;

    private readonly InMemoryDuplexStream clientEnd;

    private readonly CancellationTokenSource stopped = new();

    private readonly Task serving;

    public TlsServerConnection(SwsHttpServerConnection server, TlsServerOptions options)
    {
        (clientEnd, InMemoryDuplexStream serverEnd) = InMemoryDuplexStream.CreatePair();
        RemoteEndPoint = server.RemoteEndPoint;
        LocalEndPoint = server.LocalEndPoint;
        serving = ServeAsync(server, serverEnd, options, stopped.Token);
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
        await stopped.CancelAsync().ConfigureAwait(false);
        await serving.ConfigureAwait(false);
        stopped.Dispose();
    }

    // Whatever ends the exchange - a failed handshake, the client closing or vanishing, a
    // cancelled wait - ends this connection as a closed socket would; the client sees the end
    // of its stream. A failed handshake, such as plain HTTP sent to the HTTPS port (test1244),
    // is a reset instead, as stunnel resets it, so curl reports exit 56 "Recv failure:
    // Connection was reset" rather than exit 52 for an empty reply (GF-0081).
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Any failure ends the connection, as stunnel drops it.")]
    private static async Task ServeAsync(SwsHttpServerConnection server, InMemoryDuplexStream serverEnd, TlsServerOptions options, CancellationToken cancellationToken)
    {
        await Task.Yield();
        try
        {
            serverEnd.ResetsWhenDisposed = true;
            await using Stream tls = await TlsServerStream.AcceptAsync(serverEnd, options, TlsServerStream.PlatformServesTls13, cancellationToken).ConfigureAwait(false);
            serverEnd.ResetsWhenDisposed = false;
            await RelayAsync(server, tls, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
        }

        // The catch takes every failure, so this runs as a finally would, without the
        // rethrow branches an awaiting finally compiles to.
        await serverEnd.DisposeAsync().ConfigureAwait(false);
        await server.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task RelayAsync(SwsHttpServerConnection server, Stream tls, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[RelayBufferSize];
        int read;
        while ((read = await tls.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await server.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            // A read of an unfinished request waits for the client's next record, which only this loop delivers.
            while (!server.AwaitsRequestBytes && (read = await server.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await tls.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            if (!server.ReadsRequests)
            {
                // stunnel sends close_notify when sws closes, so curl sees a clean end of the stream.
                await TlsServerStream.ShutdownAsync(tls).ConfigureAwait(false);
                return;
            }
        }
    }
}
