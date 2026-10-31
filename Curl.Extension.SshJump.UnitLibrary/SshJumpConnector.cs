using Curl.Protocol.Abstractions;
using Curl.Ssh;

namespace Curl.Extension.SshJump;

/// <summary>
/// Opens every connection of a transfer through the SSH jump host (ADR-1167): curl's connector reaches the jump
/// host, <see cref="SshJumpSession" /> opens a <c>direct-tcpip</c> channel from there to the URL's host and port,
/// and a TLS target runs curl's TLS handshake over the channel, offering curl's ALPN list for an <c>https://</c>
/// one (ADR-1055, ADR-1066). SFTP and SCP run their own SSH session over the channel, as OpenSSH's
/// <c>ProxyJump</c> does.
/// </summary>
/// <param name="jumpHost">The jump host.</param>
/// <param name="curlConnector">The connector curl would use, which reaches the jump host.</param>
/// <param name="services">The file system, environment, random numbers and agent the session draws on.</param>
/// <param name="tlsProvider">
/// The TLS provider curl built from the option group's TLS options; with <see langword="null" />, a host that
/// hands none, a TLS target is refused as not built in.
/// </param>
internal sealed class SshJumpConnector(SshJumpHost jumpHost, IConnector curlConnector, SshJumpServices services, ITlsProvider? tlsProvider = null) : IConnector
{
    /// <inheritdoc />
    public IReadOnlyList<string> HttpOverTlsApplicationProtocols => curlConnector.HttpOverTlsApplicationProtocols;

    /// <inheritdoc />
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.UseTls && tlsProvider is null)
        {
            return ConnectResult.Failed(CurlExitCode.NotBuiltIn, $"TLS through the SSH jump host {jumpHost} is not built in");
        }

        ConnectResult reached = await curlConnector.ConnectAsync(
            new ConnectTarget(jumpHost.Host, jumpHost.Port, UseTls: false) { Events = target.Events, DiagnosticLog = target.DiagnosticLog },
            cancellationToken).ConfigureAwait(false);
        return reached.Connection is { } jumpConnection
            ? await TunnelAsync(jumpConnection, target, cancellationToken).ConfigureAwait(false)
            : reached;
    }

    /// <summary>Tells whether <paramref name="exception" /> is the jump host's failure, which fails the connect rather than the run.</summary>
    /// <param name="exception">What the session threw.</param>
    /// <returns>
    /// <see langword="true" /> for a failed handshake, host key check, authentication or channel open, and for a
    /// jump host that closed or broke the protocol.
    /// </returns>
    internal static bool IsJumpHostFailure(Exception exception) =>
        exception is SshTransferException or EndOfStreamException or InvalidDataException or SshConnectionLostException;

    // The session to the jump host and its channel to the target, then curl's TLS over the channel for a TLS target.
    private async ValueTask<ConnectResult> TunnelAsync(IConnection jumpConnection, ConnectTarget target, CancellationToken cancellationToken)
    {
        SshJumpConnection tunnel;
        try
        {
            tunnel = new SshJumpConnection(await SshJumpSession.OpenChannelAsync(jumpConnection, jumpHost, target, services, cancellationToken).ConfigureAwait(false), jumpConnection);
        }
        catch (Exception exception) when (IsJumpHostFailure(exception))
        {
            await jumpConnection.DisposeAsync().ConfigureAwait(false);
            return ConnectResult.Failed(
                exception is SshTransferException transfer ? transfer.ExitCode : CurlExitCode.CouldntConnect,
                $"SSH jump host {jumpHost}: {exception.Message}");
        }

        target.Events.ReportInfo($"Connected to {target.Host} port {target.Port} through the SSH jump host {jumpHost}");
        return target.UseTls
            ? await tlsProvider!.AuthenticateAsClientAsync(tunnel, target.TlsServerName ?? target.Host, target.Events, ApplicationProtocolsFor(target), cancellationToken).ConfigureAwait(false)
            : ConnectResult.Connected(tunnel);
    }

    /// <summary>
    /// Gives what the handshake for <paramref name="target" /> offers through ALPN, as curl's <c>TcpConnector</c>
    /// chooses it for an origin: the target's own list or else <see cref="HttpOverTlsApplicationProtocols" /> for an
    /// <c>https</c> pool scheme, and nothing for any other TLS scheme.
    /// </summary>
    /// <param name="target">The target whose handshake is about to run.</param>
    /// <returns>The protocols to offer, in preference order.</returns>
    internal IReadOnlyList<string> ApplicationProtocolsFor(ConnectTarget target) =>
        string.Equals(target.PoolScheme, "https", StringComparison.OrdinalIgnoreCase)
            ? target.ApplicationProtocols ?? HttpOverTlsApplicationProtocols
            : [];
}
