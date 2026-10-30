using Curl.Extension.DnsQuery.Client;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.DnsQuery.Transport.Https;

/// <summary>
/// Sends a query over DNS over HTTPS (RFC 8484, ADR-1145): opens a TCP connection to the server, port 443 by default,
/// runs the TLS handshake through curl's TLS provider checking the certificate against the server's host (section 5),
/// POSTs the query to <c>/dns-query</c> as <c>application/dns-message</c> over HTTP/1.1 (section 4.1) and reads the
/// 200 answer's body (section 4.2).
/// </summary>
/// <param name="connector">Opens the plaintext TCP connection.</param>
/// <param name="tlsProvider">Runs the TLS handshake over it.</param>
internal sealed class DnsHttpsTransport(IConnector connector, ITlsProvider tlsProvider) : IDnsQueryTransport
{
    /// <summary>The HTTPS port, which RFC 8484 section 5 leaves DoH on.</summary>
    public const int Port = 443;

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
            await secure.WriteAsync(DohRequest.Encode(host, port, query), context.CancellationToken).ConfigureAwait(false);
            await secure.FlushAsync(context.CancellationToken).ConfigureAwait(false);
            return await DohResponseReader.ReadAnswerAsync(secure, context.CancellationToken).ConfigureAwait(false);
        }
    }
}
