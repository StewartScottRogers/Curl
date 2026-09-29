using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;
using Curl.Quic;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// Opens QUIC connections for <see cref="TcpConnector.ConnectMultiplexedAsync" /> (ADR-0144
/// section 3, ADR-0180): for each resolved address in turn it opens a UDP channel through
/// <see cref="IUdpChannelOpener" />, runs <c>Curl.Quic</c>'s handshake with the hand-built TLS
/// client over it, and returns the <see cref="QuicConnection" /> once the handshake completes.
/// The server's certificate is judged by the same <see cref="ServerCertificateVerification" />
/// the TCP providers use, so <c>-k</c>, <c>--cacert</c>, exit 60 and exit 77 read the same.
/// </summary>
public sealed class QuicDialer
{
    // curl_easy_strerror's text for each exit a QUIC connect can fail with, which curl's
    // "QUIC connect to" and "Failed to connect to" lines end with.
    private static readonly Dictionary<CurlExitCode, string> ExitCodeWords = new()
    {
        [CurlExitCode.CouldntConnect] = "Could not connect to server",
        [CurlExitCode.WeirdServerReply] = "Weird server reply",
        [CurlExitCode.SslConnectError] = "SSL connect error",
        [CurlExitCode.SendError] = "Failed sending data to the peer",
        [CurlExitCode.RecvError] = "Failure when receiving data from the peer",
        [CurlExitCode.PeerFailedVerification] = "SSL peer certificate or SSH remote key was not OK",
    };

    private readonly IUdpChannelOpener _channelOpener;
    private readonly TlsClientOptions _options;
    private readonly bool _matchesSchannelBuild;
    private readonly TimeProvider _timeProvider;
    private readonly ITlsRandomSource _random;
    private readonly ServerCertificateVerification _verification;

    /// <summary>
    /// Creates the dialer for the curl build this platform usually runs, opening real UDP
    /// sockets bound to <paramref name="localAddress" /> and <paramref name="localPort" />.
    /// </summary>
    /// <param name="options">The TLS settings that judge the server's certificate (<c>-k</c>, <c>--cacert</c>, <c>--capath</c>).</param>
    /// <param name="timeProvider">The clock the handshake, its timeouts and the timings run on.</param>
    /// <param name="localAddress">The local address <c>--interface</c> gives, or <see langword="null" /> for any.</param>
    /// <param name="localPort">The local port <c>--local-port</c> gives, or <c>0</c> for an ephemeral one.</param>
    public QuicDialer(TlsClientOptions options, TimeProvider timeProvider, IPAddress? localAddress = null, int localPort = 0)
        : this(new UdpChannelOpener(localAddress, localPort), options, OperatingSystem.IsWindows(), timeProvider, SystemTlsRandomSource.Instance)
    {
    }

    /// <summary>
    /// Creates the dialer for a named curl build over any channel opener, so a test runs the
    /// handshake against an in-memory server.
    /// </summary>
    /// <param name="channelOpener">Opens the channel to each address.</param>
    /// <param name="options">The TLS settings that judge the server's certificate.</param>
    /// <param name="matchesSchannelBuild"><see langword="true" /> for the Schannel build's messages and Winsock wording.</param>
    /// <param name="timeProvider">The clock the handshake, its timeouts and the timings run on.</param>
    /// <param name="random">Where connection IDs, the TLS client random and key shares come from.</param>
    internal QuicDialer(
        IUdpChannelOpener channelOpener,
        TlsClientOptions options,
        bool matchesSchannelBuild,
        TimeProvider timeProvider,
        ITlsRandomSource random)
    {
        _channelOpener = channelOpener ?? throw new ArgumentNullException(nameof(channelOpener));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _matchesSchannelBuild = matchesSchannelBuild;
        _verification = new ServerCertificateVerification(options, matchesSchannelBuild, timeProvider);
    }

    /// <summary>
    /// Tries each address in turn until a QUIC handshake completes. A failure moves on to the
    /// next address unless it is a timeout (exit 28 or 55) or an unusable <c>--cacert</c>
    /// (exit 77); when every address fails, the last failure is the result.
    /// </summary>
    /// <param name="request">What to dial.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The open connection with its timings, or curl's exit code and message.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was cancelled.</exception>
    internal async ValueTask<MultiplexedConnectResult> DialAsync(QuicDialRequest request, CancellationToken cancellationToken)
    {
        var lastFailure = default(MultiplexedConnectResult);
        foreach (var address in request.Addresses)
        {
            var remaining = request.ConnectTimeout - _timeProvider.GetElapsedTime(request.Started);
            if (remaining <= TimeSpan.Zero)
            {
                return TimedOut(request);
            }

            var (result, tryNextAddress) = await AttemptAsync(request, new IPEndPoint(address, request.Port), remaining, cancellationToken).ConfigureAwait(false);
            if (!tryNextAddress)
            {
                return result;
            }

            lastFailure = result;
        }

        return lastFailure!;
    }

    // One address: curl's Trying line and trust anchors, the channel, the handshake.
    private async ValueTask<(MultiplexedConnectResult Result, bool TryNextAddress)> AttemptAsync(
        QuicDialRequest request,
        IPEndPoint endPoint,
        TimeSpan? handshakeTimeout,
        CancellationToken cancellationToken)
    {
        var events = request.Target.Events;
        events.ReportInfo($"  Trying {endPoint}...");
        events.ReportTlsTrust(SslStreamTlsProvider.DescribeTrust(_options));
        var (verifier, unusable) = CreateVerifier(request.Target.Host);
        if (verifier is null)
        {
            var (exitCode, message) = _verification.TrustAnchorsUnusable(unusable!);
            return (MultiplexedConnectResult.Failed(exitCode, message), false);
        }

        var (channel, openFailure) = OpenChannel(endPoint);
        if (channel is null)
        {
            return (Failed(request, endPoint, openFailure!), true);
        }

        var settings = new QuicClientSettings { Tls = QuicClientSettings.CreateCurlTlsSettings(HandBuiltTlsProvider.ServerNameFor(request.Target.Host)) };
        var handshake = new QuicClientHandshake(settings, _random, verifier, _timeProvider);
        var (failure, cancellation) = await RunHandshakeAsync(handshake, channel, handshakeTimeout, cancellationToken).ConfigureAwait(false);
        if (failure is null && cancellation is null)
        {
            return (Connected(request, endPoint, handshake, channel, verifier), false);
        }

        handshake.Dispose();
        await channel.DisposeAsync().ConfigureAwait(false);
        cancellation?.Throw();
        return (Failed(request, endPoint, failure!), LeavesTimeForTheNextAddress(failure!));
    }

    // The verifier over the --cacert or system anchors and the --crlfile lists, or what
    // reading them threw.
    private (HandBuiltCertificateVerifier? Verifier, Exception? Unusable) CreateVerifier(string targetHost)
    {
        try
        {
            var (chainPolicy, anchorsBesideSystemStore, revocationLists) = _verification.ReadTrustAnchors();
            return (new HandBuiltCertificateVerifier(_verification, chainPolicy, anchorsBesideSystemStore, revocationLists, targetHost), null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            return (null, exception);
        }
    }

    // A socket that cannot be opened or bound fails the attempt as a connect that reached nothing.
    private (IDatagramChannel? Channel, QuicHandshakeFailure? Failure) OpenChannel(IPEndPoint endPoint)
    {
        try
        {
            return (_channelOpener.Open(endPoint), null);
        }
        catch (SocketException exception)
        {
            return (null, new QuicHandshakeFailure(CurlExitCode.CouldntConnect, ConnectFailureReason.Describe(exception, _matchesSchannelBuild)));
        }
    }

    // The channel's socket failing is curl's recvfrom() failure, exit 56; a cancellation is
    // handed back for the caller to rethrow once it has disposed the handshake and channel.
    private async ValueTask<(QuicHandshakeFailure? Failure, ExceptionDispatchInfo? Cancellation)> RunHandshakeAsync(
        QuicClientHandshake handshake,
        IDatagramChannel channel,
        TimeSpan? handshakeTimeout,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await new QuicClientConnector(_timeProvider).RunHandshakeAsync(handshake, channel, handshakeTimeout, cancellationToken).ConfigureAwait(false), null);
        }
        catch (SocketException exception)
        {
            return (new QuicHandshakeFailure(CurlExitCode.RecvError, $"QUIC: recvfrom() unexpectedly returned -1 (errno={exception.ErrorCode}; {SocketFailureReason(exception)})"), null);
        }
        catch (OperationCanceledException exception)
        {
            return (null, ExceptionDispatchInfo.Capture(exception));
        }
    }

    // A timeout used the connect's time up; any other failure leaves the next address worth trying.
    private static bool LeavesTimeForTheNextAddress(QuicHandshakeFailure failure) =>
        failure.ExitCode is not (CurlExitCode.OperationTimedOut or CurlExitCode.SendError);

    // curl.se's Windows build words WSAECONNRESET "Connection was reset" here (measured, ADR-0144).
    private string SocketFailureReason(SocketException exception) =>
        _matchesSchannelBuild && exception.SocketErrorCode == SocketError.ConnectionReset
            ? "Connection was reset"
            : ConnectFailureReason.Describe(exception, _matchesSchannelBuild);

    private MultiplexedConnectResult Connected(
        QuicDialRequest request,
        IPEndPoint endPoint,
        QuicClientHandshake handshake,
        IDatagramChannel channel,
        HandBuiltCertificateVerifier verifier)
    {
        var handshakeCompleted = _timeProvider.GetTimestamp();
        var events = request.Target.Events;
        events.ReportTlsHandshake(new TlsHandshakeEvent
        {
            ProtocolVersion = SslProtocols.Tls13,
            CipherSuite = (TlsCipherSuite)handshake.Tls.CipherSuite!.Code,
            NegotiatedApplicationProtocol = handshake.Tls.ApplicationProtocol,

            // curl's ngtcp2 build prints no ALPN lines for QUIC (measured, ADR-0144).
            OfferedApplicationProtocols = [],
            ServerCertificate = HandBuiltTlsProvider.ServerCertificateOf(verifier.PeerCertificates),
            CertificateVerified = verifier.Observed.Verified,
            CertificateVerifyResult = verifier.Observed.VerifyResult,
            PeerCertificateChain = [.. verifier.Observed.Chain.Select(der => X509CertificateLoader.LoadCertificate(der.Span))],
            VerifiedHostName = SslStreamTlsProvider.VerifiedHostName(request.Target.Host, _options.Insecure),
        });
        events.ReportConnectionOpened(new ConnectionOpenedEvent
        {
            HostName = request.DestinationHost,
            RemoteEndPoint = endPoint,
            LocalEndPoint = channel.LocalEndPoint as IPEndPoint ?? new IPEndPoint(IPAddress.Any, 0),
            ConnectionNumber = request.ConnectionNumber,
        });

        return MultiplexedConnectResult.Connected(
            new QuicConnection(handshake, channel, _timeProvider),
            new ConnectTimings(request.Started, request.NameResolved, handshakeCompleted, handshakeCompleted));
    }

    // curl's lines for a failed QUIC connect (measured, ADR-0144): the failure, "QUIC connect
    // to" unless it was the handshake timeout, and "Failed to connect to". A certificate the
    // verifier rejected keeps the exit and message the TCP path gives it.
    private MultiplexedConnectResult Failed(QuicDialRequest request, IPEndPoint endPoint, QuicHandshakeFailure failure)
    {
        if (failure.ExitCode == CurlExitCode.OperationTimedOut)
        {
            return TimedOut(request);
        }

        var (exitCode, message) = failure.TlsFailure?.CertificateRejection is ValueTuple<CurlExitCode, string> rejected
            ? rejected
            : (failure.ExitCode, failure.Message);
        var words = ExitCodeWords[exitCode];
        var events = request.Target.Events;
        events.ReportInfo(message);
        if (exitCode != CurlExitCode.SendError)
        {
            events.ReportInfo($"QUIC connect to {endPoint.Address} port {endPoint.Port} failed: {words}");
        }

        var elapsedMilliseconds = (long)_timeProvider.GetElapsedTime(request.NameResolved).TotalMilliseconds;
        events.ReportInfo($"Failed to connect to {request.Target.Host} port {request.Target.Port} after {elapsedMilliseconds} ms: {words}");
        return MultiplexedConnectResult.Failed(exitCode, message);
    }

    // --connect-timeout counts from the connect's start, across every address (ADR-0117).
    private MultiplexedConnectResult TimedOut(QuicDialRequest request)
    {
        var message = $"Connection timed out after {(long)_timeProvider.GetElapsedTime(request.Started).TotalMilliseconds} milliseconds";
        request.Target.Events.ReportInfo(message);
        return MultiplexedConnectResult.Failed(CurlExitCode.OperationTimedOut, message);
    }
}
