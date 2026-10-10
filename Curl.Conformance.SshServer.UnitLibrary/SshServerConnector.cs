using System.Collections.Concurrent;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh;

namespace Curl.Conformance.SshServer;

/// <summary>
/// The upstream case runner's stand-in for OpenSSH's <c>sshd</c> (ADR-0456): every connection
/// is an in-memory pipe whose server end runs identification, <c>curve25519-sha256</c> key
/// exchange and the <c>ssh-userauth</c> service request with the client. Authentication and
/// the SCP and SFTP subsystems are still to come.
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

    /// <inheritdoc />
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        (SshServerDuplexConnection client, SshServerDuplexConnection server) = SshServerDuplexConnection.CreatePair();
        SshServerTransport transport = new(server, randomSource);
        sessions.Enqueue(RunSessionAsync(transport));
        return ValueTask.FromResult(ConnectResult.Connected(client));
    }

    private static async Task<SshServerTransport> RunSessionAsync(SshServerTransport transport)
    {
        await transport.ExchangeIdentificationAsync(CancellationToken.None).ConfigureAwait(false);
        await transport.ExchangeKeysAsync(CancellationToken.None).ConfigureAwait(false);
        await transport.AcceptServiceRequestAsync(CancellationToken.None).ConfigureAwait(false);
        return transport;
    }
}
