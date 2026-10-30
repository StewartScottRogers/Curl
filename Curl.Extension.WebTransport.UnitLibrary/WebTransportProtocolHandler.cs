using Curl.Extension.WebTransport.Session;
using Curl.Http3;
using Curl.HttpDatagram;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WebTransport;

/// <summary>
/// Serves <c>webtransport://host[:port]/path[?query]</c> (ADR-1089): dials HTTP/3 over QUIC, opens a
/// WebTransport session at <c>https://host[:port]/path</c>, sends the <c>-d</c> data on a bidirectional
/// stream (the default, <c>wt=bidi</c>), a unidirectional stream (<c>wt=uni</c>) or in a datagram
/// (<c>wt=datagram</c>), and writes the server's answer.
/// </summary>
public sealed class WebTransportProtocolHandler : IProtocolHandler
{
    private readonly Func<WebTransportRequest, CancellationToken, ValueTask<IMultiplexedConnection>> dial;

    /// <summary>Initializes a handler that dials through curl's datagram connector and judges certificates with curl's TLS.</summary>
    /// <param name="connector">Opens the UDP channel.</param>
    /// <param name="tlsProvider">Judges the server's certificate chain.</param>
    public WebTransportProtocolHandler(IDatagramConnector connector, ITlsProvider tlsProvider)
        : this(new WebTransportQuicDialer(connector, CertificateVerifierFactory(tlsProvider), TimeProvider.System).DialAsync)
    {
    }

    /// <summary>Gives the factory that makes each host's certificate verifier over curl's TLS.</summary>
    /// <param name="tlsProvider">Judges the server's certificate chain.</param>
    /// <returns>The factory, taking the host name.</returns>
    internal static Func<string, TlsProviderCertificateVerifier> CertificateVerifierFactory(ITlsProvider tlsProvider) =>
        host => new TlsProviderCertificateVerifier(tlsProvider, host);

    /// <summary>Initializes a handler that dials with <paramref name="dial" />, so a test runs it over an in-memory connection.</summary>
    /// <param name="dial">Gives the HTTP/3 connection for a request.</param>
    internal WebTransportProtocolHandler(Func<WebTransportRequest, CancellationToken, ValueTask<IMultiplexedConnection>> dial) => this.dial = dial;

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes { get; } = ["webtransport"];

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            return await TransferAsync(context).ConfigureAwait(false);
        }
        catch (WebTransportException failure)
        {
            return TransferResult.Failure(failure.ExitCode, failure.Message);
        }
        catch (Exception failure) when (failure is IOException or Http3Exception or QpackException or HttpDatagramException)
        {
            return TransferResult.Failure(CurlExitCode.RecvError, $"WebTransport session failed: {failure.Message}");
        }
    }

    private async ValueTask<TransferResult> TransferAsync(ITransferContext context)
    {
        WebTransportRequest request = WebTransportRequest.Of(context.Url, context.PostData);
        await using IMultiplexedConnection connection = await dial(request, context.CancellationToken).ConfigureAwait(false);
        context.Progress.ReportTransferStarted();
        long written = await new WebTransportSession(connection, context).RunAsync(request).ConfigureAwait(false);
        await connection.CloseAsync((long)Http3ErrorCode.NoError, context.CancellationToken).ConfigureAwait(false);
        return TransferResult.Success(written);
    }
}
