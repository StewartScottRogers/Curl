using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// An sws emulation connection read as a socket to sws is read: a read with no reply waiting,
/// while sws still reads requests on the connection, waits for the client's next write and reads
/// again, as sws blocks reading the next request on a kept-alive connection, instead of ending the
/// stream. A <see cref="TlsRelayConnection"/> reads its server eagerly, so without this the
/// <see cref="HttpsProxyServerConnector"/> would end the TLS connection after the first reply and
/// curl would send its next request through the proxy twice (test1586, BL-2241). Once sws no
/// longer reads requests - it closed, or a <c>CONNECT</c> handed the connection to a tunnelled
/// server - a read returns what the connection returns.
/// </summary>
internal sealed class SwsKeptAliveConnection(SwsHttpServerConnection server) : IConnection
{
    private TaskCompletionSource nextWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint => server.RemoteEndPoint;

    public EndPoint? LocalEndPoint => server.LocalEndPoint;

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        while (true)
        {
            // Taken before the read, so a write landing during it is not missed.
            Task written = Volatile.Read(ref nextWrite).Task;
            int read = await server.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read > 0 || !server.ReadsRequests)
            {
                return read;
            }

            await written.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        await server.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        Interlocked.Exchange(ref nextWrite, new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => server.DisposeAsync();
}
