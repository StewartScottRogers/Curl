using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// A stand-in server's connection as a dripping server shows it to the client: each read waits
/// <c>byteDelay</c> on the server's clock, then gives at most one byte of what the wrapped
/// connection has to read. A read cancelled during the wait takes nothing. Everything else passes
/// to the wrapped connection as it is.
/// </summary>
/// <param name="server">The connection whose bytes are given one at a time; disposed with this one.</param>
/// <param name="byteDelay">How long each read waits.</param>
/// <param name="clock">The clock the waits are on.</param>
internal sealed class OneBytePerReadConnection(IConnection server, TimeSpan byteDelay, TimeProvider clock) : IConnection
{
    public bool IsSecure => server.IsSecure;

    public EndPoint? RemoteEndPoint => server.RemoteEndPoint;

    public EndPoint? LocalEndPoint => server.LocalEndPoint;

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        await Task.Delay(byteDelay, clock, cancellationToken).ConfigureAwait(false);
        return await server.ReadAsync(buffer[..Math.Min(buffer.Length, 1)], cancellationToken).ConfigureAwait(false);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        server.WriteAsync(buffer, cancellationToken);

    public ValueTask FlushAsync(CancellationToken cancellationToken) => server.FlushAsync(cancellationToken);

    public ValueTask DisposeAsync() => server.DisposeAsync();
}
