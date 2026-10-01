using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Kerberos;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="IKerberosKdcProxyTransport" /> (BL-882): one HTTPS POST to an
/// MS-KKDCP proxy, an <c>https://</c> <c>kdc</c> entry, made as MIT's <c>sendto_kdc.c</c> makes
/// it. The connection comes from an <see cref="IConnector" /> asked for plain TCP, with no
/// pooling, and its TLS is this transport's own (<see cref="IKerberosKdcProxyTlsClient" />, no
/// ALPN): the proxy's certificate must name its host and lead to the realm's
/// <c>http_anchors</c> roots, or to the system's trust store when there are none, so the
/// transfer's <c>-k</c>, <c>--cacert</c> and <c>--capath</c> never apply (ADR-0300, BL-1063).
/// The request is <c>POST /path HTTP/1.0</c> with MIT's headers and the
/// <c>KDC-PROXY-MESSAGE</c> as its body, and the reply is read until the proxy closes the
/// connection. Every failure to get a <c>200</c> body back within the exchange timeout, an
/// anchor that cannot be loaded included, is an <see cref="IOException" />, so the sender tries
/// the realm's next KDC.
/// </summary>
public sealed class KerberosKdcProxyHttpsTransport : IKerberosKdcProxyTransport
{
    /// <summary>The largest reply read, headers included: MIT's limit for a stream reply.</summary>
    public const int MaximumReplyLength = 1024 * 1024;

    private static readonly byte[] HeaderEnd = "\r\n\r\n"u8.ToArray();

    private readonly IConnector _connector;

    private readonly TimeSpan _exchangeTimeout;

    private readonly TimeProvider _timeProvider;

    private readonly IKerberosKdcProxyTlsClient _tlsClient;

    private readonly Func<string, string?> _readEnvironmentVariable;

    /// <summary>Creates the transport with <c>SslStream</c>-based TLS and the process's environment for <c>ENV:</c> anchors.</summary>
    /// <param name="connector">Opens the TCP connection to the proxy.</param>
    /// <param name="exchangeTimeout">How long the whole exchange, connection included, may take.</param>
    /// <param name="timeProvider">Times the wait.</param>
    public KerberosKdcProxyHttpsTransport(IConnector connector, TimeSpan exchangeTimeout, TimeProvider timeProvider)
        : this(connector, exchangeTimeout, timeProvider, new KerberosKdcProxyTlsClient(), Environment.GetEnvironmentVariable)
    {
    }

    /// <summary>Creates the transport with the TLS client and environment given.</summary>
    /// <param name="connector">Opens the TCP connection to the proxy.</param>
    /// <param name="exchangeTimeout">How long the whole exchange, connection included, may take.</param>
    /// <param name="timeProvider">Times the wait.</param>
    /// <param name="tlsClient">Secures the connection and verifies the proxy's certificate.</param>
    /// <param name="readEnvironmentVariable">Reads an <c>ENV:</c> anchor's variable, <see langword="null" /> when unset.</param>
    internal KerberosKdcProxyHttpsTransport(
        IConnector connector,
        TimeSpan exchangeTimeout,
        TimeProvider timeProvider,
        IKerberosKdcProxyTlsClient tlsClient,
        Func<string, string?> readEnvironmentVariable)
    {
        _connector = connector;
        _exchangeTimeout = exchangeTimeout;
        _timeProvider = timeProvider;
        _tlsClient = tlsClient;
        _readEnvironmentVariable = readEnvironmentVariable;
    }

    /// <inheritdoc />
    public Task<byte[]> PostAsync(string host, int port, string path, ReadOnlyMemory<byte> body, CancellationToken cancellationToken) =>
        PostAsync(host, port, path, [], body, cancellationToken);

    /// <inheritdoc />
    public async Task<byte[]> PostAsync(string host, int port, string path, IReadOnlyList<string> httpAnchors, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        X509Certificate2Collection? anchors = httpAnchors.Count == 0 ? null : KerberosHttpAnchorLoader.Load(httpAnchors, _readEnvironmentVariable);
        using CancellationTokenSource timeout = new(_exchangeTimeout, _timeProvider);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            byte[] reply = await ExchangeAsync(host, port, anchors, BuildRequest(host, path, body), linked.Token).ConfigureAwait(false);
            return ReadBody(host, port, reply);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new IOException($"The KDC proxy {host} port {port} did not answer within {_exchangeTimeout.TotalSeconds:0.###} s.");
        }
    }

    /// <summary>
    /// Builds MIT's request (<c>make_proxy_request</c>): the request line, <c>Host</c>, the
    /// no-cache headers, MIT's <c>User-Agent</c>, the Kerberos content type and length, then the body.
    /// </summary>
    /// <param name="host">The proxy's host, bracketed in <c>Host</c> when it is an IPv6 address.</param>
    /// <param name="path">The proxy's path without its leading slash.</param>
    /// <param name="body">The encoded <c>KDC-PROXY-MESSAGE</c>.</param>
    /// <returns>The request bytes.</returns>
    internal static byte[] BuildRequest(string host, string path, ReadOnlyMemory<byte> body)
    {
        string hostHeader = host.Contains(':', StringComparison.Ordinal) ? $"[{host}]" : host;
        byte[] head = Encoding.ASCII.GetBytes(
            $"POST /{path} HTTP/1.0\r\n" +
            $"Host: {hostHeader}\r\n" +
            "Cache-Control: no-cache\r\n" +
            "Pragma: no-cache\r\n" +
            "User-Agent: kerberos/1.0\r\n" +
            "Content-type: application/kerberos\r\n" +
            $"Content-Length: {body.Length}\r\n" +
            "\r\n");
        return [.. head, .. body.Span];
    }

    private async Task<byte[]> ExchangeAsync(string host, int port, X509Certificate2Collection? anchors, byte[] request, CancellationToken cancellationToken)
    {
        ConnectResult connected = await _connector.ConnectAsync(new ConnectTarget(host, port, UseTls: false), cancellationToken).ConfigureAwait(false);
        IConnection connection = connected.Connection ?? throw new IOException(connected.ErrorMessage);
        Stream secured = await _tlsClient.AuthenticateAsync(new ConnectionStream(connection, ownsConnection: true), host, anchors, cancellationToken).ConfigureAwait(false);
        await using (secured.ConfigureAwait(false))
        {
            await secured.WriteAsync(request, cancellationToken).ConfigureAwait(false);
            await secured.FlushAsync(cancellationToken).ConfigureAwait(false);
            return await ReadToEndAsync(host, port, secured, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<byte[]> ReadToEndAsync(string host, int port, Stream secured, CancellationToken cancellationToken)
    {
        using MemoryStream reply = new();
        byte[] buffer = new byte[16384];
        int read;
        while ((read = await secured.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            reply.Write(buffer, 0, read);
            if (reply.Length > MaximumReplyLength)
            {
                throw new IOException($"The KDC proxy {host} port {port} sent a reply longer than {MaximumReplyLength} bytes.");
            }
        }

        return reply.ToArray();
    }

    /// <summary>
    /// Takes the body from a whole reply, as MIT's <c>service_https_read</c> does: the status
    /// line must be <c>HTTP/1.x 200</c>, and the body is everything after the blank line.
    /// </summary>
    /// <param name="host">The proxy's host, for the failure message.</param>
    /// <param name="port">The proxy's port, for the failure message.</param>
    /// <param name="reply">Every byte the proxy sent.</param>
    /// <returns>The reply body.</returns>
    /// <exception cref="IOException">The reply has no blank line or another status.</exception>
    internal static byte[] ReadBody(string host, int port, byte[] reply)
    {
        int headerEnd = reply.AsSpan().IndexOf(HeaderEnd);
        if (headerEnd < 0)
        {
            throw new IOException($"The KDC proxy {host} port {port} sent no complete HTTP reply.");
        }

        string head = Encoding.Latin1.GetString(reply, 0, headerEnd);
        int lineEnd = head.IndexOf("\r\n", StringComparison.Ordinal);
        string statusLine = lineEnd < 0 ? head : head[..lineEnd];
        string[] parts = statusLine.Split(' ', 3);
        if (parts.Length < 2 || !parts[0].StartsWith("HTTP/1.", StringComparison.Ordinal) || parts[1] != "200")
        {
            throw new IOException($"The KDC proxy {host} port {port} answered {statusLine}.");
        }

        return reply[(headerEnd + HeaderEnd.Length)..];
    }
}
