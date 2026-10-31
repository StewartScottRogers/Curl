using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.SshJump;

/// <summary>The <c>--ssh-jump</c> connection hook: every connection opens through the jump host, and no datagram socket opens at all.</summary>
/// <param name="jumpHost">The jump host <c>--ssh-jump</c> named.</param>
/// <param name="services">The file system, environment, random numbers and agent the session draws on.</param>
internal sealed class SshJumpConnectionHook(SshJumpHost jumpHost, SshJumpServices services) : IConnectionHook
{
    /// <inheritdoc />
    public ExtensionOption Option => SshJumpExtension.Option;

    /// <summary>Gets the jump host the hook connects through.</summary>
    public SshJumpHost JumpHost => jumpHost;

    /// <inheritdoc />
    public IConnector ConnectorOver(IConnector curlConnector) => new SshJumpConnector(jumpHost, curlConnector, services);

    /// <inheritdoc />
    public IConnector ConnectorOver(IConnector curlConnector, ITlsProvider curlTlsProvider) =>
        new SshJumpConnector(jumpHost, curlConnector, services, curlTlsProvider);

    /// <inheritdoc />
    public IDatagramConnector DatagramConnectorOver(IDatagramConnector curlDatagramConnector) => new SshJumpDatagramConnector(jumpHost);
}
