using System.Buffers.Binary;
using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ldap;

/// <summary>
/// The SASL security layer WinLDAP puts over the session once a logon bind succeeds (RFC 4422
/// 3.7, ADR-0166, measured by BL-853): each write is sealed with the bind's keys and sent as one
/// buffer, its length in four big-endian octets and then the sealed bytes; each buffer read is
/// checked and unsealed, and its bytes are what the reads beneath return.
/// </summary>
/// <param name="connection">The connection to the LDAP server, which the layer does not dispose.</param>
/// <param name="authentication">The complete authentication whose keys seal and unseal, which the layer disposes.</param>
/// <remarks>
/// A buffer that does not unseal, that says it is longer than
/// <see cref="MaxWrappedLength" />, or that the server closes part way through, reads as the
/// server closing: WinLDAP drops the connection on the first and fails the search with
/// <c>Server Down</c>, as it does when the server closes.
/// </remarks>
internal sealed class LdapSaslSecurityLayer(IConnection connection, ILdapLogonAuthentication authentication) : IConnection
{
    /// <summary>The longest sealed buffer read: 16 MiB, far beyond any reply WinLDAP seals.</summary>
    internal const int MaxWrappedLength = 16 * 1024 * 1024;

    /// <summary>How many octets carry a buffer's length.</summary>
    private const int LengthOctets = 4;

    private byte[] unsealed = [];

    private int unsealedRead;

    /// <inheritdoc />
    public bool IsSecure => connection.IsSecure;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => connection.RemoteEndPoint;

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        while (unsealedRead == unsealed.Length)
        {
            if (await ReadUnsealedBufferAsync(cancellationToken).ConfigureAwait(false) is not { } next)
            {
                return 0;
            }

            unsealed = next;
            unsealedRead = 0;
        }

        int count = Math.Min(buffer.Length, unsealed.Length - unsealedRead);
        unsealed.AsSpan(unsealedRead, count).CopyTo(buffer.Span);
        unsealedRead += count;
        return count;
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        byte[] wrapped = authentication.Wrap(buffer.Span);
        byte[] framed = new byte[LengthOctets + wrapped.Length];
        BinaryPrimitives.WriteInt32BigEndian(framed, wrapped.Length);
        wrapped.CopyTo(framed, LengthOctets);
        await connection.WriteAsync(framed, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => connection.FlushAsync(cancellationToken);

    /// <summary>Disposes the authentication whose keys the layer seals with; the connection beneath stays open.</summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        authentication.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Reads one sealed buffer and unseals it; <see langword="null" /> when the server closed
    /// first, the length is too long, or the buffer does not unseal.
    /// </summary>
    private async ValueTask<byte[]?> ReadUnsealedBufferAsync(CancellationToken cancellationToken)
    {
        byte[] length = new byte[LengthOctets];
        if (!await ReadExactlyAsync(length, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        uint wrappedLength = BinaryPrimitives.ReadUInt32BigEndian(length);
        if (wrappedLength > MaxWrappedLength)
        {
            return null;
        }

        byte[] wrapped = new byte[wrappedLength];
        return await ReadExactlyAsync(wrapped, cancellationToken).ConfigureAwait(false)
            ? authentication.Unwrap(wrapped)
            : null;
    }

    /// <summary>Fills <paramref name="destination" />; <see langword="false" /> when the server closes first.</summary>
    private async ValueTask<bool> ReadExactlyAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        int filled = 0;
        while (filled < destination.Length)
        {
            int read = await connection.ReadAsync(destination[filled..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }

            filled += read;
        }

        return true;
    }
}
