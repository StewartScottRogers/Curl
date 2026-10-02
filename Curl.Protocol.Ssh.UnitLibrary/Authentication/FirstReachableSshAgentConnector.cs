namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// Tries agents in order and connects to the first that can be reached, as libssh2 1.11.1's
/// <c>libssh2_agent_connect</c> walks its <c>supported_backends</c>: a later agent is not
/// tried once one connects.
/// </summary>
/// <param name="connectors">The agents, first tried first.</param>
internal sealed class FirstReachableSshAgentConnector(IReadOnlyList<ISshAgentConnector> connectors) : ISshAgentConnector
{
    /// <summary>Gets the agents, first tried first.</summary>
    internal IReadOnlyList<ISshAgentConnector> Connectors => connectors;

    /// <inheritdoc />
    public async ValueTask<Stream?> ConnectAsync(CancellationToken cancellationToken)
    {
        foreach (ISshAgentConnector connector in connectors)
        {
            if (await connector.ConnectAsync(cancellationToken).ConfigureAwait(false) is { } connection)
            {
                return connection;
            }
        }

        return null;
    }
}
