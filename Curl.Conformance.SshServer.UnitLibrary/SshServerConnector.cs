using System.Collections.Concurrent;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh;

namespace Curl.Conformance.SshServer;

/// <summary>
/// The upstream case runner's stand-in for OpenSSH's <c>sshd</c> (ADR-0456): every connection
/// is an in-memory pipe whose server end exchanges SSH identification strings with the
/// client. Key exchange, authentication and the SCP and SFTP subsystems are still to come.
/// </summary>
/// <param name="randomSource">Where packet padding comes from.</param>
public sealed class SshServerConnector(ISshRandomSource randomSource) : IConnector
{
    private readonly ConcurrentQueue<Task<SshServerTransport>> sessions = new();

    /// <summary>
    /// Gets each connection's session so far, in connection order: a task that completes with
    /// the server's transport once the identification exchange is done.
    /// </summary>
    internal IReadOnlyList<Task<SshServerTransport>> Sessions => [.. sessions];

    /// <inheritdoc />
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        (SshServerDuplexConnection client, SshServerDuplexConnection server) = SshServerDuplexConnection.CreatePair();
        SshServerTransport transport = new(server, randomSource);
        sessions.Enqueue(IdentifyAsync(transport));
        return ValueTask.FromResult(ConnectResult.Connected(client));
    }

    private static async Task<SshServerTransport> IdentifyAsync(SshServerTransport transport)
    {
        await transport.ExchangeIdentificationAsync(CancellationToken.None).ConfigureAwait(false);
        return transport;
    }
}
