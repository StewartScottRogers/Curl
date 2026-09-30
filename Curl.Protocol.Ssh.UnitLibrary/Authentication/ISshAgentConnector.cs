namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// Opens the connection to the user's ssh-agent that <see cref="SshUserAuthentication" />
/// asks for identities and signatures, so tests can answer with a fake agent.
/// </summary>
internal interface ISshAgentConnector
{
    /// <summary>Connects to the agent.</summary>
    /// <param name="cancellationToken">Cancels the connection.</param>
    /// <returns>The connection, or <see langword="null" /> when no agent can be reached.</returns>
    ValueTask<Stream?> ConnectAsync(CancellationToken cancellationToken);
}
