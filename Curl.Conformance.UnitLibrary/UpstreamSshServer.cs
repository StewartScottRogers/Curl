using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// What the case runner needs of the stand-in for upstream's test <c>sshd</c>, which lives in
/// <c>Curl.Conformance.SshServer.UnitLibrary</c> because it reuses the SSH client's wire code
/// this library may not reference (ADR-0456): the caller builds it from there and hands it to
/// <see cref="UpstreamCaseRunner"/> (BL-1916).
/// </summary>
/// <param name="CreateServer">Makes a fresh server for one case; connections to <see cref="SshPort"/> reach it.</param>
/// <param name="User">The user the server lets in, <c>%USER</c>.</param>
/// <param name="ClientPrivateKeyFile">The client's private key file, written as <c>%LOGDIR/server/curl_client_key</c>.</param>
/// <param name="ClientPublicKeyFile">The client's public key file, written as <c>%LOGDIR/server/curl_client_key.pub</c>.</param>
/// <param name="HostKeyMd5">The server's host key MD5 fingerprint, <c>%SSHSRVMD5</c>.</param>
/// <param name="HostKeySha256">The server's host key SHA-256 fingerprint, <c>%SSHSRVSHA256</c>.</param>
public sealed record UpstreamSshServer(
    Func<IConnector> CreateServer,
    string User,
    ReadOnlyMemory<byte> ClientPrivateKeyFile,
    ReadOnlyMemory<byte> ClientPublicKeyFile,
    string HostKeyMd5,
    string HostKeySha256)
{
    /// <summary>The value of <c>%SSHPORT</c>.</summary>
    public const int SshPort = 9003;

    /// <summary>
    /// Puts the server on <see cref="SshPort"/> in front of the case's other servers.
    /// </summary>
    /// <param name="otherPorts">Where every connection to another port goes.</param>
    /// <returns>A connector routing by port.</returns>
    public IConnector InFrontOf(IConnector otherPorts) => new SshPortConnector(CreateServer(), otherPorts);

    private sealed class SshPortConnector(IConnector sshServer, IConnector otherPorts) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            (target.Port == SshPort ? sshServer : otherPorts).ConnectAsync(target, cancellationToken);
    }
}
