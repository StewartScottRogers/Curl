using Curl.Extension.Protocol.DnsQuery.Client;
using Curl.Extension.Protocol.DnsQuery.Transport.Tcp;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.DnsQuery.Transport.Tls;

/// <summary>
/// Sends a query over DNS over TLS (RFC 7858): opens a TCP connection to the server, port 853 by default (section
/// 3.1), runs the TLS handshake through curl's TLS provider before any DNS message is sent, checking the certificate
/// against the server's host (section 3.2), then sends the query and reads its answer each with the two-octet length
/// prefix of DNS over TCP (section 3.3).
/// </summary>
/// <param name="connector">Opens the plaintext TCP connection.</param>
/// <param name="tlsProvider">Runs the TLS handshake over it.</param>
internal sealed class DnsTlsTransport(IConnector connector, ITlsProvider tlsProvider) : IDnsQueryTransport
{
    /// <summary>The port RFC 7858 section 3.1 assigns to DNS over TLS.</summary>
    public const int Port = 853;

    /// <inheritdoc />
    public int DefaultPort => Port;

    /// <inheritdoc />
    public async ValueTask<byte[]> ExchangeAsync(string host, int port, byte[] query, ITransferContext context)
    {
        ConnectResult tcp = await connector.ConnectAsync(new ConnectTarget(host, port, UseTls: false), context.CancellationToken).ConfigureAwait(false);
        if (tcp.Connection is not { } plaintext)
        {
            throw new DnsQueryException(tcp.ExitCode, tcp.ErrorMessage!);
        }

        ConnectResult tls = await tlsProvider.AuthenticateAsClientAsync(plaintext, host, context.CancellationToken).ConfigureAwait(false);
        if (tls.Connection is not { } secure)
        {
            await plaintext.DisposeAsync().ConfigureAwait(false);
            throw new DnsQueryException(tls.ExitCode, tls.ErrorMessage!);
        }

        await using (secure.ConfigureAwait(false))
        {
            return await DnsTcpTransport.ExchangeOverAsync(secure, query, context.CancellationToken).ConfigureAwait(false);
        }
    }
}
