using Curl.Extended.Protocol.DnsQuery.Client;
using Curl.Extended.Protocol.DnsQuery.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.DnsQuery.Transport.Https;

/// <summary>
/// Sends a query over DNS over HTTPS (RFC 8484, ADR-1148): opens a TCP connection to the server, port 443 by default,
/// runs the TLS handshake through curl's TLS provider checking the certificate against the server's host (section 5),
/// POSTs the query to <c>/dns-query</c> as <c>application/dns-message</c> over HTTP/1.1 with DNS ID 0 (section 4.1) and reads the
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

    /// <summary>Gets true: a DoH query carries DNS ID 0 for cache friendliness (RFC 8484 section 4.1).</summary>
    public bool QueriesWithMessageIdZero => true;

    /// <summary>The protocols the TLS handshake offers through ALPN: HTTP/2 first, RFC 8484 section 5.2's minimum recommended version, then HTTP/1.1 (BL-4690, ADR-1231).</summary>
    public static IReadOnlyList<string> ApplicationProtocols { get; } = [DohHttp2Exchange.Protocol, "http/1.1"];

    /// <inheritdoc />
    public async ValueTask<byte[]> ExchangeAsync(string host, int port, byte[] query, ITransferContext context)
    {
        byte[] padded = DnsEdnsPadding.Pad(query);
        ConnectResult tcp = await connector.ConnectAsync(new ConnectTarget(host, port, UseTls: false), context.CancellationToken).ConfigureAwait(false);
        if (tcp.Connection is not { } plaintext)
        {
            throw new DnsQueryException(tcp.ExitCode, tcp.ErrorMessage!);
        }

        ConnectResult tls = await tlsProvider.AuthenticateAsClientAsync(plaintext, host, context.Events, ApplicationProtocols, context.CancellationToken).ConfigureAwait(false);
        if (tls.Connection is not { } secure)
        {
            await plaintext.DisposeAsync().ConfigureAwait(false);
            throw new DnsQueryException(tls.ExitCode, tls.ErrorMessage!);
        }

        await using (secure.ConfigureAwait(false))
        {
            if (tls.ApplicationProtocol == DohHttp2Exchange.Protocol)
            {
                return await DohHttp2Exchange.ExchangeAsync(secure, host, port, padded, context.CancellationToken).ConfigureAwait(false);
            }

            await secure.WriteAsync(DohRequest.Encode(host, port, padded), context.CancellationToken).ConfigureAwait(false);
            await secure.FlushAsync(context.CancellationToken).ConfigureAwait(false);
            return await DohResponseReader.ReadAnswerAsync(secure, context.CancellationToken).ConfigureAwait(false);
        }
    }
}
