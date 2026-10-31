using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Ssh;
using Curl.Ssh.Authentication;
using Curl.Ssh.Connection;
using Curl.Ssh.HostKeys;
using Curl.Ssh.KeyExchange;
using Curl.Ssh.Keys;
using Curl.Ssh.Negotiation;
using Curl.Ssh.Transport;

namespace Curl.Extension.SshJump;

/// <summary>
/// The SSH session to the jump host, built from <c>Curl.Ssh</c>'s transport, host key check and user
/// authentication (ADR-1166, ADR-1167): the handshake (RFC 4253), the host key checked against the user's
/// known-hosts file before anything is authenticated, the <c>ssh-userauth</c> service and the user's
/// password, ssh-agent and key files (RFC 4252), then one <c>direct-tcpip</c> channel to the transfer's
/// host and port (RFC 4254 section 7.2).
/// </summary>
internal static class SshJumpSession
{
    /// <summary>
    /// The originator address the channel open names, as OpenSSH's <c>ssh -W</c> (what <c>ProxyJump</c> runs)
    /// names it for a connection with no local socket behind it.
    /// </summary>
    internal const string OriginatorAddress = "127.0.0.1";

    /// <summary>The originator port the channel open names, as OpenSSH's <c>ssh -W</c> names it.</summary>
    internal const int OriginatorPort = 65535;

    /// <summary>Runs the session over <paramref name="connection" /> and opens the channel to <paramref name="target" />.</summary>
    /// <param name="connection">The connection to the jump host, which the session reads and writes.</param>
    /// <param name="jumpHost">The jump host and its user.</param>
    /// <param name="target">The host and port the jump host connects to.</param>
    /// <param name="services">The file system, environment, random numbers and agent.</param>
    /// <param name="cancellationToken">Cancels the session.</param>
    /// <returns>The open channel.</returns>
    /// <exception cref="SshTransferException">
    /// The handshake, the host key check or the authentication failed, with curl's exit code for it, or the
    /// jump host refused the channel (exit 7).
    /// </exception>
    /// <exception cref="EndOfStreamException">The jump host closed the connection.</exception>
    /// <exception cref="InvalidDataException">The jump host broke the protocol.</exception>
    internal static async ValueTask<SshDirectTcpipChannel> OpenChannelAsync(
        IConnection connection, SshJumpHost jumpHost, ConnectTarget target, SshJumpServices services, CancellationToken cancellationToken)
    {
        SshOptions options = new() { KnownHostsPath = services.KnownHostsPath() };
        KnownHostsFile knownHosts = await KnownHostsFile.LoadAsync(services.FileSystem, options.KnownHostsPath, cancellationToken).ConfigureAwait(false);
        SshAlgorithmPreferences offered = SshHostKeyChecker.NarrowHostKeys(SshAlgorithmPreferences.Full, jumpHost.Host, jumpHost.Port, options, knownHosts);
        SshTransport transport = new(connection, offered, SshAlgorithmCatalogue.Implemented, services.RandomSource, services.EphemeralKeySource);
        SshNegotiatedHandshake handshake = await transport.NegotiateAlgorithmsAsync(cancellationToken).ConfigureAwait(false);
        SshKeyExchangeResult keys = await transport.ExchangeKeysAsync(handshake, cancellationToken).ConfigureAwait(false);
        SshHostKeyChecker.Check(keys.HostKey, jumpHost.Host, jumpHost.Port, options, knownHosts);
        SshUserKeySource userKeys = new(services.FileSystem, services.ReadEnvironmentVariable, options, Encoding.UTF8);
        SshUserAuthentication authentication = new(transport, Encoding.UTF8, userKeys, agentConnector: services.AgentConnector);
        await authentication.RequestServiceAsync(cancellationToken).ConfigureAwait(false);
        await authentication.AuthenticateAsync(new NetworkCredential(jumpHost.User, jumpHost.Password), cancellationToken).ConfigureAwait(false);
        SshDirectTcpipChannel channel = new(transport, target.Host, target.Port);
        return await channel.OpenAsync(OriginatorAddress, OriginatorPort, cancellationToken).ConfigureAwait(false)
            ? channel
            : throw new SshTransferException(
                CurlExitCode.CouldntConnect,
                $"the jump host refused to connect to {target.Host} port {target.Port} (RFC 4254 reason code {channel.OpenFailureReasonCode})");
    }
}
