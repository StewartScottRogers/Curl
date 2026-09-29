using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ldap.Fakes;

/// <summary>
/// An <see cref="IConnection" /> that plays an LDAP server from a script as
/// <see cref="ScriptedConnection" /> does, then resets: once <paramref name="reads" /> is
/// exhausted every read throws an <see cref="IOException" />, and every write after the first
/// <paramref name="writesBeforeReset" /> throws one, as a socket reset by the server does.
/// </summary>
/// <param name="writesBeforeReset">How many writes succeed before the reset reaches them.</param>
/// <param name="reads">What the server sends before it resets, one read at a time.</param>
public sealed class ResettingConnection(int writesBeforeReset, params byte[][] reads) : IConnection
{
    private readonly ScriptedConnection script = new(reads);

    private int writesDone;

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <summary>Gets every byte written before the reset, in order.</summary>
    public byte[] Sent => script.Sent;

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int read = await script.ReadAsync(buffer, cancellationToken);
        if (read == 0)
        {
            throw new IOException("Connection reset by peer.");
        }

        return read;
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (writesDone == writesBeforeReset)
        {
            throw new IOException("Connection reset by peer.");
        }

        writesDone++;
        return script.WriteAsync(buffer, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
