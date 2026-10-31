using System.Buffers.Binary;
using System.Net;
using Curl.Extension.EncryptedDns.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.EncryptedDns.DnsOverTls;

/// <summary>
/// Looks host names up over DNS over TLS (RFC 7858): opens a TCP connection to the server, port 853 by default
/// (section 3.1), runs the TLS handshake before any DNS message is sent (section 3.2), then sends the AAAA and the
/// A query pipelined on the one connection and reads both answers, matched by message ID, each message with the
/// two-octet length prefix of DNS over TCP (section 3.3, RFC 1035 section 4.2.2).
/// </summary>
/// <param name="connector">Opens the plaintext TCP connection to the server.</param>
/// <param name="tlsProvider">Runs the TLS handshake over that connection.</param>
/// <param name="server">The server to ask.</param>
public sealed class DnsOverTlsResolver(IConnector connector, ITlsProvider tlsProvider, DnsOverTlsServer server) : IDnsResolver
{
    /// <summary>The message ID of the A query.</summary>
    internal const ushort AQueryId = 1;

    /// <summary>The message ID of the AAAA query.</summary>
    internal const ushort AaaaQueryId = 2;

    private const int LengthPrefixLength = 2;

    /// <summary>
    /// Resolves <paramref name="host" />: an address literal as itself, any other name through the server.
    /// </summary>
    /// <param name="host">The host name or literal address to resolve.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>
    /// The AAAA answer's addresses, then the A answer's; none when the name cannot be encoded, the connection or
    /// handshake fails, the server ends the connection early, or both answers fail.
    /// </returns>
    public async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        if (IPAddress.TryParse(host.Trim('[', ']'), out IPAddress? literal))
        {
            return [literal];
        }

        byte[]? queries = EncodeQueries(host);
        return queries is null ? [] : await AskServerAsync(host, queries, cancellationToken).ConfigureAwait(false);
    }

    // Both queries, each with its length prefix, AAAA first; null for a name RFC 1035 cannot carry.
    private static byte[]? EncodeQueries(string host)
    {
        try
        {
            return [.. WithLengthPrefix(DnsQueryEncoder.Encode(AaaaQueryId, host, DnsRecordType.Aaaa)),
                .. WithLengthPrefix(DnsQueryEncoder.Encode(AQueryId, host, DnsRecordType.A))];
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static byte[] WithLengthPrefix(byte[] message)
    {
        byte[] prefixed = new byte[LengthPrefixLength + message.Length];
        BinaryPrimitives.WriteUInt16BigEndian(prefixed, (ushort)message.Length);
        message.CopyTo(prefixed, LengthPrefixLength);
        return prefixed;
    }

    private async ValueTask<IReadOnlyList<IPAddress>> AskServerAsync(string host, byte[] queries, CancellationToken cancellationToken)
    {
        ConnectResult tcp = await connector.ConnectAsync(new ConnectTarget(server.Host, server.Port, UseTls: false), cancellationToken).ConfigureAwait(false);
        if (tcp.Connection is null)
        {
            return [];
        }

        ConnectResult tls = await tlsProvider.AuthenticateAsClientAsync(tcp.Connection, server.AuthenticationName, cancellationToken).ConfigureAwait(false);
        if (tls.Connection is null)
        {
            return [];
        }

        await using IConnection connection = tls.Connection;
        try
        {
            await connection.WriteAsync(queries, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            return await ReadAnswersAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return [];
        }
    }

    // Reads two answers, in whatever order the server sends them (RFC 7858 section 3.3), and keeps each by its ID.
    private static async ValueTask<IReadOnlyList<IPAddress>> ReadAnswersAsync(IConnection connection, CancellationToken cancellationToken)
    {
        Dictionary<ushort, DnsRecordType> pending = new() { [AaaaQueryId] = DnsRecordType.Aaaa, [AQueryId] = DnsRecordType.A };
        Dictionary<DnsRecordType, IReadOnlyList<IPAddress>> answers = [];
        for (int read = 0; read < 2; read++)
        {
            byte[] message = await ReadMessageAsync(connection, cancellationToken).ConfigureAwait(false);
            ushort id = message.Length >= 2 ? BinaryPrimitives.ReadUInt16BigEndian(message) : (ushort)0;
            if (pending.Remove(id, out DnsRecordType type))
            {
                answers[type] = DnsAnswerDecoder.Decode(message, id, type).Addresses;
            }
        }

        return [.. answers.GetValueOrDefault(DnsRecordType.Aaaa, []), .. answers.GetValueOrDefault(DnsRecordType.A, [])];
    }

    private static async ValueTask<byte[]> ReadMessageAsync(IConnection connection, CancellationToken cancellationToken)
    {
        byte[] prefix = await ReadExactlyAsync(connection, LengthPrefixLength, cancellationToken).ConfigureAwait(false);
        return await ReadExactlyAsync(connection, BinaryPrimitives.ReadUInt16BigEndian(prefix), cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<byte[]> ReadExactlyAsync(IConnection connection, int count, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[count];
        int filled = 0;
        while (filled < count)
        {
            int read = await connection.ReadAsync(buffer.AsMemory(filled), cancellationToken).ConfigureAwait(false);
            filled += read > 0 ? read : throw new EndOfStreamException("The DNS-over-TLS server closed the connection inside a message.");
        }

        return buffer;
    }
}
