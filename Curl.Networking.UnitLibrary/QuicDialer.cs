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
        [CurlExitCode.InterfaceFailed] = "Failed binding local connection end",
        [CurlExitCode.BadFunctionArgument] = "A libcurl function was given a bad argument",
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
    /// <param name="options">The TLS settings: the ones that judge the server's certificate (<c>-k</c>, <c>--cacert</c>, <c>--capath</c>), <c>--cert</c> and the cipher options.</param>
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
    /// <param name="options">The TLS settings: the ones that judge the server's certificate, <c>--cert</c> and the cipher options.</param>
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

            var (result, tryNextAddress) = await AttemptAsync(request, new IPEndPoint(address, request.Port), remaining, tunnel: null, cancellationToken).ConfigureAwait(false);
            if (!tryNextAddress)
            {
                return result;
            }

            lastFailure = result;
        }

        return lastFailure!;
    }

    /// <summary>
    /// Runs the QUIC handshake over <paramref name="tunnel" />, a CONNECT-UDP tunnel through an
    /// HTTP proxy at <paramref name="proxyEndPoint" /> (BL-942): no <c>Trying</c> line, as
    /// curl 8.22.0 prints none once the tunnel is up (measured), and the tunnel disposed when
    /// the handshake does not connect.
    /// </summary>
    /// <param name="request">The target, its destination and the connect's clock.</param>
    /// <param name="tunnel">The datagram channel the tunnel carries.</param>
    /// <param name="proxyEndPoint">The proxy's address, which the failure lines name.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The QUIC connection, or the handshake's failure.</returns>
    internal async ValueTask<MultiplexedConnectResult> DialThroughTunnelAsync(
        QuicDialRequest request,
        IDatagramChannel tunnel,
        IPEndPoint proxyEndPoint,
        CancellationToken cancellationToken)
    {
        var remaining = request.ConnectTimeout - _timeProvider.GetElapsedTime(request.Started);
        var (result, _) = remaining <= TimeSpan.Zero
            ? (TimedOut(request), false)
            : await AttemptAsync(request, proxyEndPoint, remaining, tunnel, cancellationToken).ConfigureAwait(false);
        if (result.Connection is null)
        {
            await tunnel.DisposeAsync().ConfigureAwait(false);
        }

        return result;
    }

    // One address: curl's Trying line (none through a tunnel), the ClientHello's suites and
    // --cert certificate, then the connect; the certificate is disposed once the handshake no
    // longer needs it.
    private async ValueTask<(MultiplexedConnectResult Result, bool TryNextAddress)> AttemptAsync(
        QuicDialRequest request,
        IPEndPoint endPoint,
        TimeSpan? handshakeTimeout,
        IDatagramChannel? tunnel,
        CancellationToken cancellationToken)
    {
        if (tunnel is null)
        {
            request.Target.Events.ReportInfo($"  Trying {endPoint}...");
        }

        var (tls, loadedCertificate, preparationFailure) = PrepareTls(request.Target.Host);
        using var clientCertificate = loadedCertificate;
        return tls is null
            ? (preparationFailure!, false)
            : await ConnectAsync(request, endPoint, tls, handshakeTimeout, tunnel, cancellationToken).ConfigureAwait(false);
    }

    // The trust anchors, the channel (the tunnel when there is one) and the handshake, once
    // the ClientHello is ready.
    private async ValueTask<(MultiplexedConnectResult Result, bool TryNextAddress)> ConnectAsync(
        QuicDialRequest request,
        IPEndPoint endPoint,
        Tls13ClientSettings tls,
        TimeSpan? handshakeTimeout,
        IDatagramChannel? tunnel,
        CancellationToken cancellationToken)
    {
        // curl.se's ngtcp2 build binds the socket before it names the trust anchors (measured, BL-1025).
        var (channel, openFailure) = await OpenChannelAsync(request, endPoint, tunnel, cancellationToken).ConfigureAwait(false);
        if (channel is null)
        {
            return (openFailure!, true);
        }

        request.Target.Events.ReportTlsTrust(DescribeTrust());
        var (verifier, unusable) = CreateVerifier(request.Target.Host);
        if (verifier is null)
        {
            return (await TrustAnchorsUnusableAsync(channel, tunnel, unusable!).ConfigureAwait(false), false);
        }

        var handshake = new QuicClientConnectionState(new QuicClientSettings { Tls = tls }, _random, verifier, _timeProvider);
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

    // The failure for trust anchors or revocation lists that cannot be read; the socket opened is closed,
    // and a tunnel left to DialThroughTunnelAsync, which closes it.
    private async ValueTask<MultiplexedConnectResult> TrustAnchorsUnusableAsync(IDatagramChannel channel, IDatagramChannel? tunnel, Exception unusable)
    {
        if (tunnel is null)
        {
            await channel.DisposeAsync().ConfigureAwait(false);
        }

        var (exitCode, message) = _verification.TrustAnchorsUnusable(unusable);
        return MultiplexedConnectResult.Failed(exitCode, message);
    }

    // What the ClientHello offers, in the order HandBuiltTlsProvider prepares it: the suites,
    // then the --cert certificate. Both QUIC builds run on the OpenSSL API (ADR-0290): curl.se's
    // LibreSSL build on Windows sends its measured hello, the OpenSSL build elsewhere its own.
    private (Tls13ClientSettings? Tls, X509Certificate2? ClientCertificate, MultiplexedConnectResult? Failure) PrepareTls(string targetHost)
    {
        var (suites, cipherFailure) = SelectCipherSuites();
        if (cipherFailure is not null)
        {
            return (null, null, MultiplexedConnectResult.Failed(CurlExitCode.SslCipher, cipherFailure));
        }

        var (certificate, certificateFailure) = ClientCertificateLoader.LoadAsOpenSslBuild(_options, recognisesDriveLetters: _matchesSchannelBuild);
        if (certificateFailure is not null)
        {
            return (null, null, MultiplexedConnectResult.Failed(certificateFailure.ExitCode, certificateFailure.ErrorMessage!));
        }

        var serverName = HandBuiltTlsProvider.ServerNameFor(targetHost);
        var profile = _matchesSchannelBuild ? QuicClientSettings.CreateLibreSslTlsSettings(serverName) : QuicClientSettings.CreateOpenSslTlsSettings(serverName);
        var tls = profile with
        {
            CipherSuites = suites ?? profile.CipherSuites,
            ClientCertificate = HandBuiltTlsProvider.ToTlsClientCertificate(certificate),
        };
        return (tls, certificate, null);
    }

    // --ciphers and --tls13-ciphers as the OpenSSL build reads them (ADR-0011), cut to the
    // TLS 1.3 suites QUIC protects packets with; a list leaving none is exit 59 with
    // HandBuiltTlsProvider's message.
    private (IReadOnlyList<ushort>? Suites, string? FailureMessage) SelectCipherSuites()
    {
        var (suites, failureMessage) = OpenSslCipherSuites.Select(_options.Ciphers, _options.Tls13Ciphers);
        if (suites is null)
        {
            return (null, failureMessage);
        }

        ushort[] offered = [.. suites.Select(suite => (ushort)suite).Where(QuicPacketProtection.CanProtect)];
        return offered.Length == 0
            ? (null, OpenSslCipherSuites.Unapplied(_options.Ciphers, _options.Tls13Ciphers))
            : (offered, null);
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

    // The tunnel when there is one; else a new socket, bound as --interface and --local-port ask.
    // A local end that cannot be bound fails the attempt as the TCP path's does (BindFailed); a
    // socket that cannot be opened, as a connect that reached nothing.
    private async ValueTask<(IDatagramChannel? Channel, MultiplexedConnectResult? Failure)> OpenChannelAsync(
        QuicDialRequest request,
        IPEndPoint endPoint,
        IDatagramChannel? tunnel,
        CancellationToken cancellationToken)
    {
        if (tunnel is not null)
        {
            return (tunnel, null);
        }

        try
        {
            return (await OpenBoundChannelAsync(request.LocalBinding, endPoint, cancellationToken).ConfigureAwait(false), null);
        }
        catch (LocalBindException exception)
        {
            return (null, BindFailed(request, exception.Failure));
        }
        catch (SocketException exception)
        {
            var failure = new QuicHandshakeFailure(CurlExitCode.CouldntConnect, ConnectFailureReason.Describe(exception, _matchesSchannelBuild));
            return (null, Failed(request, endPoint, failure));
        }
    }

    // Unbound without a LocalBinding; else on the address chosen for the family dialled and the
    // first free port of the --local-port range, as libcurl's bindlocal binds a QUIC socket. Its -v
    // bind lines are not written for QUIC yet: the TCP path's (BL-1027) were not measured over QUIC.
    private async ValueTask<IDatagramChannel> OpenBoundChannelAsync(LocalBindingAddressChooser? localBinding, IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        if (localBinding is null)
        {
            return _channelOpener.Open(endPoint);
        }

        var binding = localBinding.Binding;
        if ((binding.InterfaceName ?? binding.DeviceName) is { } deviceName)
        {
            // An --interface name is bound as a device first, as LocalBindingTcpDialer binds TCP's (BL-1077).
            return await _channelOpener.OpenFromDeviceAsync(
                endPoint,
                deviceName,
                bindsAddressAfterDevice: binding.InterfaceName is null,
                token => ChooseLocalEndAsync(localBinding, endPoint.AddressFamily, token),
                binding.PortCount,
                cancellationToken).ConfigureAwait(false);
        }

        var localEndPoint = await ChooseLocalEndAsync(localBinding, endPoint.AddressFamily, cancellationToken).ConfigureAwait(false);
        return _channelOpener.OpenFrom(endPoint, localEndPoint, binding.PortCount);
    }

    private static async ValueTask<IPEndPoint> ChooseLocalEndAsync(LocalBindingAddressChooser localBinding, AddressFamily family, CancellationToken cancellationToken)
    {
        var localAddress = await localBinding.ChooseAsync(family, NoTransferEvents.Instance, cancellationToken).ConfigureAwait(false);
        return new IPEndPoint(localAddress, localBinding.Binding.FirstPort);
    }

    // A local end that could not be bound, as curl.se's ngtcp2 build ends it (measured, BL-1025):
    // no "QUIC connect to" line, only "Failed to connect to", with exit 45 and exit 43 worded as
    // the TCP path words them and a local address of the other family the usual exit 7.
    private MultiplexedConnectResult BindFailed(QuicDialRequest request, LocalBindFailure failure)
    {
        var exitCode = failure switch
        {
            LocalBindFailure.InterfaceFailed => CurlExitCode.InterfaceFailed,
            LocalBindFailure.BadArgument => CurlExitCode.BadFunctionArgument,
            _ => CurlExitCode.CouldntConnect,
        };
        var elapsedMilliseconds = (long)_timeProvider.GetElapsedTime(request.NameResolved).TotalMilliseconds;
        var message = $"Failed to connect to {request.Target.Host} port {request.Target.Port} after {elapsedMilliseconds} ms: {ExitCodeWords[exitCode]}";
        request.Target.Events.ReportInfo(message);
        return MultiplexedConnectResult.Failed(exitCode, message);
    }

    // The channel's socket failing is curl's recvfrom() failure, exit 56; a cancellation is
    // handed back for the caller to rethrow once it has disposed the handshake and channel.
    private async ValueTask<(QuicHandshakeFailure? Failure, ExceptionDispatchInfo? Cancellation)> RunHandshakeAsync(
        QuicClientConnectionState handshake,
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

    // On Windows, without --cacert, Curl verifies against the Windows stores, which curl.se's
    // LibreSSL build names as --ca-native makes it (measured, BL-1050); its embedded bundle's
    // "CA Blob from configuration" is not printed, as Curl embeds none (ADR-0144).
    private TlsTrustEvent DescribeTrust()
    {
        var trust = SslStreamTlsProvider.DescribeTrust(_options) with { IsQuic = true };
        return _matchesSchannelBuild && _options.CaCertificateFile is null
            ? trust with { CaCertificateFile = null, UsesWindowsSystemStores = true }
            : trust;
    }

    private MultiplexedConnectResult Connected(
        QuicDialRequest request,
        IPEndPoint endPoint,
        QuicClientConnectionState handshake,
        IDatagramChannel channel,
        HandBuiltCertificateVerifier verifier)
    {
        var handshakeCompleted = _timeProvider.GetTimestamp();
        var events = request.Target.Events;
        verifier.Observed.ReportVerifyResult(events, isProxy: false, _matchesSchannelBuild);
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
            IsQuic = true,
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
