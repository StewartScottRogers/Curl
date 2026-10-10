using System.Collections.Concurrent;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh;

namespace Curl.Conformance.SshServer;

/// <summary>
/// The upstream case runner's stand-in for OpenSSH's <c>sshd</c> (ADR-0456): every connection
/// is an in-memory pipe whose server end runs identification, <c>curve25519-sha256</c> key
/// exchange, the <c>ssh-userauth</c> service request, user authentication and a session
/// channel with the client (BL-1953), on which an <c>scp</c> command (BL-1917) or the <c>sftp</c>
/// subsystem (BL-1918) runs.
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
        Task<SshServerSessionChannel> channel = OpenChannelAsync(session);
        channels.Enqueue(channel);
        processes.Enqueue(RunProcessesAsync(transport, channel));
        return ValueTask.FromResult(ConnectResult.Connected(client));
    }

    /// <summary>
    /// Gets each connection's processes so far, in connection order: a task that runs every
    /// <c>scp</c> command and <c>sftp</c> subsystem the connection's channels start, one channel after another as curl
    /// reuses a connection, and ends when the client opens no further channel. A channel whose
    /// <c>exec</c> or <c>subsystem</c> runs anything else is left to whoever holds it from <see cref="Channels"/>.
    /// </summary>
    internal IReadOnlyList<Task> Processes => [.. processes];

    private readonly ConcurrentQueue<Task> processes = new();

    private static async Task<SshServerSessionChannel> OpenChannelAsync(Task<SshServerTransport> session)
    {
        SshServerTransport transport = await session.ConfigureAwait(false);
        string user = await SshServerUserAuthentication.AuthenticateAsync(transport, CancellationToken.None).ConfigureAwait(false);
        return await SshServerSessionChannel.OpenAsync(transport, user, CancellationToken.None).ConfigureAwait(false);
    }

    // The first channel's scp or SFTP, then each next channel's, until the client disconnects (the
    // next open then fails) or starts something else.
    private static async Task RunProcessesAsync(SshServerTransport transport, Task<SshServerSessionChannel> first)
    {
        SshServerSessionChannel channel = await first.ConfigureAwait(false);
        while (await RunProcessAsync(channel).ConfigureAwait(false))
        {
            channel = await SshServerSessionChannel.OpenAsync(transport, channel.User, CancellationToken.None).ConfigureAwait(false);
        }
    }

    // Runs the channel's sftp subsystem or scp command to its end; false for anything else.
    private static async Task<bool> RunProcessAsync(SshServerSessionChannel channel)
    {
        if (channel.ProcessRequest == "subsystem")
        {
            if (channel.Process != SshServerSftpProcess.SubsystemName)
            {
                return false;
            }

            await SshServerSftpProcess.RunAsync(channel, CancellationToken.None).ConfigureAwait(false);
            return true;
        }

        if (SshServerScpCommand.Parse(channel.Process) is not { } command)
        {
            return false;
        }

        await SshServerScpProcess.RunAsync(channel, command, CancellationToken.None).ConfigureAwait(false);
        return true;
    }

    private static async Task<SshServerTransport> RunSessionAsync(SshServerTransport transport)
    {
        await transport.ExchangeIdentificationAsync(CancellationToken.None).ConfigureAwait(false);
        await transport.ExchangeKeysAsync(CancellationToken.None).ConfigureAwait(false);
        await transport.AcceptServiceRequestAsync(CancellationToken.None).ConfigureAwait(false);
        return transport;
    }
}
