using System.Net;
using Curl.Extended.Protocol.DnsQuery.Transport.Tcp;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.DnsQuery.Transport.Https;

/// <summary>
/// The DoH server's connection with each failed send or receive turned into a <see cref="DohConnectionException" />
/// worded as a TCP query words it (<see cref="DnsTcpTransport" />): exit 55 for a write or flush, exit 56 for a read,
/// never .NET's exception text.
/// </summary>
/// <param name="connection">The TLS connection.</param>
internal sealed class DohFailureWordingConnection(IConnection connection) : IConnection
{
    /// <inheritdoc />
    public bool IsSecure => connection.IsSecure;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => connection.RemoteEndPoint;

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        try
        {
            return await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException failure)
        {
            throw new DohConnectionException(DnsTcpTransport.ReceiveFailureOf(failure));
        }
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        try
        {
            await connection.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException failure)
        {
            throw new DohConnectionException(DnsTcpTransport.SendFailureOf(failure));
        }
    }

    /// <inheritdoc />
    public async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        try
        {
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException failure)
        {
            throw new DohConnectionException(DnsTcpTransport.SendFailureOf(failure));
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
