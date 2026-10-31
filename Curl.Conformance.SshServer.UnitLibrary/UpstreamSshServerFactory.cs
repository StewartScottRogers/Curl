using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh;
using Curl.Ssh;

namespace Curl.Conformance.SshServer;

/// <summary>
/// Builds the <see cref="UpstreamSshServer"/> the case runner puts on <c>%SSHPORT</c>: the
/// stand-in for upstream's test <c>sshd</c>, its account and its fixed keys (BL-1916, BL-2217).
/// It exists so the upstream conformance tests and the gap office's
/// <c>Gap/Tools/Measure-UpstreamCases.cs</c> build the same server in one line.
/// </summary>
public static class UpstreamSshServerFactory
{
    /// <summary>
    /// Creates the stand-in for the curl build under test. curl's WinCNG build on Windows reads
    /// no Ed25519 key and offers no <c>ssh-ed25519</c> host key, so for it the client key files
    /// are the RSA pair and the fingerprints the RSA host key's; for curl's OpenSSL build
    /// elsewhere they are the Ed25519 ones (BL-1954).
    /// </summary>
    /// <param name="windowsBuildOfCurl">Whether the curl matched is the Windows (WinCNG) build;
    /// pass <see cref="OperatingSystem.IsWindows"/> to match the platform's curl.</param>
    public static UpstreamSshServer Create(bool windowsBuildOfCurl) => windowsBuildOfCurl
        ? new(
            ServerPerCase,
            SshServerClientAccount.User,
            Encoding.ASCII.GetBytes(SshServerRsaClientKey.PrivateKeyPem),
            Encoding.ASCII.GetBytes(SshServerRsaClientKey.PublicKeyLine + "\n"),
            SshServerRsaHostKey.Md5Fingerprint,
            SshServerRsaHostKey.Sha256Fingerprint)
        {
            HostKeyAlgorithm = "ssh-rsa",
        }
        : new(
            ServerPerCase,
            SshServerClientAccount.User,
            SshServerClientAccount.CreatePrivateKeyFile(),
            SshServerClientAccount.CreatePublicKeyFile(),
            SshServerHostKey.Md5Fingerprint,
            SshServerHostKey.Sha256Fingerprint)
        {
            HostKeyAlgorithm = "ssh-ed25519",
        };

    // Held in a field, so Create has no delegate-cache branch for a test to miss.
    private static readonly Func<IConnector> ServerPerCase = () => new SshServerConnector(new SystemSshRandomSource());
}
