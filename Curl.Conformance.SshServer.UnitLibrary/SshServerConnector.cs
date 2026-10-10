using System.Collections.Concurrent;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh;

namespace Curl.Conformance.SshServer;

/// <summary>
/// The upstream case runner's stand-in for OpenSSH's <c>sshd</c> (ADR-0456): every connection
/// is an in-memory pipe whose server end runs identification, <c>curve25519-sha256</c> key
/// exchange, the <c>ssh-userauth</c> service request, user authentication and a session
/// channel with the client (BL-1953). SCP and SFTP payloads are still to come.
/// </summary>
/// <param name="randomSource">Where packet padding comes from.</param>
public sealed class SshServerConnector(ISshRandomSource randomSource) : IConnector
{
    private readonly ConcurrentQueue<Task<SshServerTransport>> sessions = new();

    /// <summary>
    /// Gets each connection's session so far, in connection order: a task that completes with
    /// the server's transport once it has accepted the <c>ssh-userauth</c> service request.
    /// </summary>
    internal IReadOnlyList<Task<SshServerTransport>> Sessions => [.. sessions];

    /// <summary>
    /// Gets each connection's session channel so far, in connection order: a task that completes
    /// once the client has authenticated as <see cref="SshServerClientAccount" />, opened a
    /// <c>session</c> channel and started a process on it with <c>exec</c> or <c>subsystem</c>.
    /// </summary>
    internal IReadOnlyList<Task<SshServerSessionChannel>> Channels => [.. channels];

    private readonly ConcurrentQueue<Task<SshServerSessionChannel>> channels = new();

    /// <inheritdoc />
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        (SshServerDuplexConnection client, SshServerDuplexConnection server) = SshServerDuplexConnection.CreatePair();
        SshServerTransport transport = new(server, randomSource);
        Task<SshServerTransport> session = RunSessionAsync(transport);
        sessions.Enqueue(session);
        channels.Enqueue(OpenChannelAsync(session));
        return ValueTask.FromResult(ConnectResult.Connected(client));
    }

    private static async Task<SshServerSessionChannel> OpenChannelAsync(Task<SshServerTransport> session)
    {
        SshServerTransport transport = await session.ConfigureAwait(false);
        string user = await SshServerUserAuthentication.AuthenticateAsync(transport, CancellationToken.None).ConfigureAwait(false);
        return await SshServerSessionChannel.OpenAsync(transport, user, CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task<SshServerTransport> RunSessionAsync(SshServerTransport transport)
    {
        await transport.ExchangeIdentificationAsync(CancellationToken.None).ConfigureAwait(false);
        await transport.ExchangeKeysAsync(CancellationToken.None).ConfigureAwait(false);
        await transport.AcceptServiceRequestAsync(CancellationToken.None).ConfigureAwait(false);
        return transport;
    }
}
