using System.Security.Cryptography;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Negotiation;

namespace Curl.Protocol.Ssh.HostKeys;

/// <summary>
/// Decides whether the server's host key is accepted, as curl 8.21.0's
/// <c>lib/vssh/libssh2.c</c> does after the handshake, and narrows the host-key list from
/// the known-hosts file before it. Measured 2026-09-29 against the Windows reference build
/// (BL-566, ADR-0213).
/// </summary>
/// <remarks>
/// <c>--hostpubsha256</c> is checked first, then <c>--hostpubmd5</c>; when either is given
/// the known-hosts file is not consulted, and <c>-k</c> changes neither. Without them the
/// known-hosts file decides, unless <c>-k</c> turned it off (a <see langword="null" />
/// <see cref="KnownHostsFile" />), when every host key is accepted.
/// </remarks>
internal static class SshHostKeyChecker
{
    /// <summary>
    /// The value libssh2 1.11.1 gives <c>LIBSSH2_KNOWNHOST_KEY_UNKNOWN</c>, which curl prints
    /// for a known-hosts entry of a type it cannot narrow to.
    /// </summary>
    internal const int UnknownKeyTypeMask = 15 << 18;

    private const string PeerFailedVerification = "SSL peer certificate or SSH remote key was not OK";

    /// <summary>
    /// Narrows the host-key list to the type of the known-hosts entry for the host, as
    /// curl does before the handshake; skipped under <c>-k</c> and when
    /// <c>--hostpubmd5</c> is given (curl skips it for that option but not for
    /// <c>--hostpubsha256</c>).
    /// </summary>
    /// <param name="preferences">The platform preset.</param>
    /// <param name="host">The URL's host name.</param>
    /// <param name="port">The port connected to.</param>
    /// <param name="options">The SSH options.</param>
    /// <param name="knownHosts">The known-hosts file, or <see langword="null" /> under <c>-k</c>.</param>
    /// <returns>The preferences, narrowed when an entry names the host.</returns>
    /// <exception cref="SshTransferException">
    /// Exit 79: <c>Found host key type RSA1 which is not supported</c> for an SSH-1 entry,
    /// <c>Unknown host key type: 3932160</c> for an entry of a type libssh2 does not
    /// recognize (<c>ssh-dss</c> and certificate types among them), or what
    /// <see cref="SshAlgorithmPreferences.NarrowHostKeysTo" /> reports.
    /// </exception>
    internal static SshAlgorithmPreferences NarrowHostKeys(
        SshAlgorithmPreferences preferences,
        string host,
        int port,
        SshOptions options,
        KnownHostsFile? knownHosts)
    {
        KnownHostsEntry? entry = options.HostPublicKeyMd5 is null ? knownHosts?.FindNarrowingEntry(host, port) : null;
        return entry is null ? preferences : preferences.NarrowHostKeysTo(NarrowingName(entry.KeyType));
    }

    /// <summary>
    /// Accepts or refuses the server's host key.
    /// </summary>
    /// <param name="hostKey">The server's host key blob, <c>K_S</c>, its signature already verified.</param>
    /// <param name="host">The URL's host name.</param>
    /// <param name="port">The port connected to.</param>
    /// <param name="options">The SSH options.</param>
    /// <param name="knownHosts">The known-hosts file, or <see langword="null" /> under <c>-k</c>.</param>
    /// <exception cref="SshTransferException">
    /// Exit 60 with <c>Denied establishing ssh session: mismatch SHA256 fingerprint. Remote
    /// &lt;base64&gt; is not equal to &lt;given&gt;</c> or its <c>MD5</c> form (lower-case
    /// hex), or with <c>SSL peer certificate or SSH remote key was not OK</c> when the
    /// known-hosts file has no entry for the host or a different key for it.
    /// </exception>
    internal static void Check(byte[] hostKey, string host, int port, SshOptions options, KnownHostsFile? knownHosts)
    {
        if (options.HostPublicKeySha256 is null && options.HostPublicKeyMd5 is null)
        {
            CheckKnownHosts(hostKey, host, port, knownHosts);
            return;
        }

        CheckSha256(hostKey, options.HostPublicKeySha256);
        CheckMd5(hostKey, options.HostPublicKeyMd5);
    }

    private static void CheckKnownHosts(byte[] hostKey, string host, int port, KnownHostsFile? knownHosts)
    {
        if (knownHosts is not null && knownHosts.Check(host, port, hostKey) != KnownHostsCheck.Match)
        {
            throw new SshTransferException(CurlExitCode.PeerFailedVerification, PeerFailedVerification);
        }
    }

    private static void CheckSha256(byte[] hostKey, string? expected)
    {
        string fingerprint = Convert.ToBase64String(SHA256.HashData(hostKey));
        if (expected is not null && expected.Split('=')[0] != fingerprint.Split('=')[0])
        {
            throw Denied("SHA256", fingerprint, expected);
        }
    }

    private static void CheckMd5(byte[] hostKey, string? expected)
    {
        string fingerprint = Convert.ToHexStringLower(MD5.HashData(hostKey));
        if (expected is not null && !string.Equals(fingerprint, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw Denied("MD5", fingerprint, expected);
        }
    }

    private static SshTransferException Denied(string hashName, string fingerprint, string expected) =>
        new(
            CurlExitCode.PeerFailedVerification,
            $"Denied establishing ssh session: mismatch {hashName} fingerprint. Remote {fingerprint} is not equal to {expected}");

    private static string NarrowingName(KnownHostKeyType type) => type switch
    {
        KnownHostKeyType.Rsa1 => throw new SshTransferException(CurlExitCode.Ssh, "Found host key type RSA1 which is not supported"),
        KnownHostKeyType.Unknown => throw new SshTransferException(CurlExitCode.Ssh, $"Unknown host key type: {UnknownKeyTypeMask}"),
        _ => KnownHostKeyTypeNames.NameOf(type),
    };
}
