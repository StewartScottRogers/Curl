using System.Security.Cryptography;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.HostKeys;

namespace Curl.Protocol.Ssh;

/// <content>The server's host key and the check's verdict (BL-4366).</content>
internal sealed partial class SshDiagnosticLog
{
    /// <summary>Logs, at <c>info</c>, the host key's type and SHA-256 fingerprint.</summary>
    /// <param name="hostKey">The server's host key blob.</param>
    internal void HostKeyPresented(byte[] hostKey)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            string fingerprint = Convert.ToBase64String(SHA256.HashData(hostKey)).TrimEnd('=');
            Write(DiagnosticLogLevel.Info, $"host key {SshKeyBlobReader.ReadKeyTypeName(hostKey)} SHA256:{fingerprint}");
        }
    }

    /// <summary>
    /// Logs the host key check's verdict: <c>warning</c> when no known-hosts file was read
    /// and the key was taken unchecked, as <c>--insecure</c> does; <c>info</c> otherwise.
    /// </summary>
    /// <param name="options">The SSH options the check ran with.</param>
    /// <param name="knownHosts">The known-hosts file, or <see langword="null" /> under <c>-k</c>.</param>
    internal void HostKeyAccepted(SshOptions options, KnownHostsFile? knownHosts)
    {
        if (options.HostPublicKeySha256 is not null || options.HostPublicKeyMd5 is not null)
        {
            WriteIfEnabled(DiagnosticLogLevel.Info, "host key accepted: it matches the fingerprint given");
        }
        else if (knownHosts is null)
        {
            WriteIfEnabled(DiagnosticLogLevel.Warning, "host key accepted unchecked: no known_hosts file (--insecure)");
        }
        else
        {
            WriteIfEnabled(DiagnosticLogLevel.Info, "host key accepted: it matches known_hosts");
        }
    }
}
