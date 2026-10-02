using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="IHandshakeReportingTlsProvider" /> that records what it was given and returns
/// either a fixed secure connection or, when <see cref="FailureToReturn" /> is set, that
/// failure. It reports no handshake itself.
/// </summary>
public sealed class FakeTlsProvider : IHandshakeReportingTlsProvider
{
    /// <summary>Gets the connection every handshake returns.</summary>
    public FakeConnection SecuredConnection { get; } = new() { IsSecure = true };

    /// <summary>
    /// Gets or sets the failed result every handshake returns instead of
    /// <see cref="SecuredConnection" />; <see langword="null" /> means the handshake succeeds.
    /// </summary>
    public ConnectResult? FailureToReturn { get; init; }

    /// <summary>Gets the route this provider says it is; <see cref="TlsClientRoute.SslStream" /> unless set.</summary>
    public TlsClientRoute Route { get; init; } = TlsClientRoute.SslStream;

    /// <summary>Gets why this provider says it was chosen; <see langword="null" /> unless set.</summary>
    public string? RouteReason { get; init; }

    /// <summary>
    /// Gets whether a successful four-argument handshake says its revocation check could not
    /// complete, as <see cref="SslStreamTlsProvider" /> does under <c>--ssl-revoke-best-effort</c>.
    /// </summary>
    public bool RevocationCheckIncomplete { get; init; }

    /// <summary>
    /// Gets the handshake a successful four-argument handshake reports on its events, as the real
    /// providers do; <see langword="null" /> reports none.
    /// </summary>
    public TlsHandshakeEvent? HandshakeToReport { get; init; }

    /// <summary>
    /// Gets or sets the timings a successful handshake reports; <see langword="null" />
    /// means it reports none.
    /// </summary>
    public ConnectTimings? TimingsToReturn { get; init; }

    /// <summary>Gets or sets the peer certificates a successful handshake reports.</summary>
    public IReadOnlyList<ReadOnlyMemory<byte>> PeerCertificatesToReturn { get; init; } = [];

    /// <summary>Gets the protocol a successful handshake says the server accepted through ALPN; <see langword="null" /> means none.</summary>
    public string? ApplicationProtocolToReturn { get; init; }

    /// <summary>Gets the plaintext connection last passed in, if any.</summary>
    public IConnection? ReceivedPlaintext { get; private set; }

    /// <summary>Gets the target host last passed in, if any.</summary>
    public string? ReceivedTargetHost { get; private set; }

    /// <summary>Gets the number of handshakes requested.</summary>
    public int HandshakeCount { get; private set; }

    /// <summary>
    /// Gets the events last passed to the reporting overload, if it was called.
    /// </summary>
    public ITransferEvents? ReceivedEvents { get; private set; }

    /// <summary>
    /// Gets the <c>isProxy</c> value of every call to the reporting overload, in order.
    /// </summary>
    public List<bool> ReceivedIsProxy { get; } = [];

    /// <summary>
    /// Gets the <c>applicationProtocols</c> value of every call to the reporting overload, in order.
    /// </summary>
    public List<IReadOnlyList<string>> ReceivedApplicationProtocols { get; } = [];

    /// <inheritdoc />
    public ValueTask<ConnectResult> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        ITransferEvents events,
        bool isProxy,
        IReadOnlyList<string> applicationProtocols,
        CancellationToken cancellationToken)
    {
        ReceivedEvents = events;
        if (HandshakeToReport is { } handshake && FailureToReturn is null)
        {
            events.ReportTlsHandshake(handshake);
        }

        if (RevocationCheckIncomplete)
        {
            (events as HandshakeCapturingTransferEvents)?.ReportRevocationCheckIncomplete();
        }

        ReceivedIsProxy.Add(isProxy);
        ReceivedApplicationProtocols.Add(applicationProtocols);
        return AuthenticateAsClientAsync(plaintext, targetHost, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<ConnectResult> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        CancellationToken cancellationToken)
    {
        HandshakeCount++;
        ReceivedPlaintext = plaintext;
        ReceivedTargetHost = targetHost;

        return ValueTask.FromResult(FailureToReturn ?? ConnectResult.Connected(
            SecuredConnection,
            TimingsToReturn,
            peerCertificates: PeerCertificatesToReturn,
            applicationProtocol: ApplicationProtocolToReturn));
    }
}
