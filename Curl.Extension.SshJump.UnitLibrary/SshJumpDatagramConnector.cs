using Curl.Protocol.Abstractions;

namespace Curl.Extension.SshJump;

/// <summary>
/// Refuses every datagram socket under <c>--ssh-jump</c> (ADR-1167): a <c>direct-tcpip</c> channel carries TCP
/// alone (RFC 4254 section 7.2), and a QUIC connection sent past the jump host would reach the server from this
/// machine, which the option is given to prevent.
/// </summary>
/// <param name="jumpHost">The jump host, named in the refusal.</param>
internal sealed class SshJumpDatagramConnector(SshJumpHost jumpHost) : IDatagramConnector
{
    /// <inheritdoc />
    public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
        ValueTask.FromResult(DatagramOpenResult.Failed(
            CurlExitCode.CouldntConnect,
            $"the SSH jump host {jumpHost} carries TCP only, so UDP to {host} port {port} is not sent"));
}
