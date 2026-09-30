using Curl.Protocol.Ssh.Authentication;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>An ssh-agent that is never there, as on the machine curl was first measured on.</summary>
internal sealed class UnreachableSshAgent : ISshAgentConnector
{
    /// <inheritdoc />
    public ValueTask<Stream?> ConnectAsync(CancellationToken cancellationToken) => ValueTask.FromResult<Stream?>(null);
}
