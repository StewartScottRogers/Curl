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
        KnownHostsFile? knownHosts) =>
        NarrowHostKeys(preferences, host, port, options, knownHosts, NoTransferEvents.Instance);

    /// <summary>
    /// Narrows the host-key list as the overload without events does, writing curl's
    /// <c>-v</c> lines on the way (ADR-0262): <c>SSH: failed to read known hosts from
    /// &lt;path&gt;</c> for a file libssh2 could not read to its end, then, unless
    /// <c>--hostpubmd5</c> is given, <c>SSH: found host '&lt;host&gt;' in '&lt;path&gt;'</c>
    /// and <c>SSH: set '&lt;names&gt;' as hostkey type</c>, or <c>SSH: did not find host
    /// '&lt;host&gt;' in '&lt;path&gt;'</c>.
    /// </summary>
    /// <param name="preferences">The platform preset.</param>
    /// <param name="host">The URL's host name.</param>
    /// <param name="port">The port connected to.</param>
    /// <param name="options">The SSH options.</param>
    /// <param name="knownHosts">The known-hosts file, or <see langword="null" /> under <c>-k</c>.</param>
    /// <param name="events">Where the lines go.</param>
    /// <returns>The preferences, narrowed when an entry names the host.</returns>
    /// <exception cref="SshTransferException">As the overload without events throws.</exception>
    internal static SshAlgorithmPreferences NarrowHostKeys(
        SshAlgorithmPreferences preferences,
        string host,
        int port,
        SshOptions options,
        KnownHostsFile? knownHosts,
        ITransferEvents events)
    {
        if (knownHosts is null)
        {
            return preferences;
        }

        string path = options.KnownHostsPath ?? string.Empty;
        if (knownHosts.ReadFailed)
        {
            events.ReportInfo(SshInfoLines.KnownHostsReadFailed(path));
        }

        if (options.HostPublicKeyMd5 is not null)
        {
            return preferences;
        }

        if (knownHosts.FindNarrowingEntry(host, port) is not { } entry)
        {
            events.ReportInfo(SshInfoLines.DidNotFindHost(host, path));
            return preferences;
        }

        events.ReportInfo(SshInfoLines.FoundHost(host, path));
        string keyType = NarrowingName(entry.KeyType);
        events.ReportInfo(SshInfoLines.HostKeyTypeSet(SshAlgorithmPreferences.NarrowedHostKeyNames(keyType)));
        return preferences.NarrowHostKeysTo(keyType);
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
    internal static void Check(byte[] hostKey, string host, int port, SshOptions options, KnownHostsFile? knownHosts) =>
        Check(hostKey, host, port, options, knownHosts, NoTransferEvents.Instance);

    /// <summary>
    /// Accepts or refuses the server's host key as the overload without events does, writing
    /// curl's <c>-v</c> lines on the way (ADR-0262): each fingerprint given and the host
    /// key's, then <c>checksum match</c>; or <c>SSH: no knownhosts file configured</c> under
    /// <c>-k</c>; or <c>SSH: host check &lt;n&gt;, key: &lt;key&gt;</c> and whether the
    /// entry matched.
    /// </summary>
    /// <param name="hostKey">The server's host key blob, <c>K_S</c>, its signature already verified.</param>
    /// <param name="host">The URL's host name.</param>
    /// <param name="port">The port connected to.</param>
    /// <param name="options">The SSH options.</param>
    /// <param name="knownHosts">The known-hosts file, or <see langword="null" /> under <c>-k</c>.</param>
    /// <param name="events">Where the lines go.</param>
    /// <exception cref="SshTransferException">As the overload without events throws.</exception>
    internal static void Check(byte[] hostKey, string host, int port, SshOptions options, KnownHostsFile? knownHosts, ITransferEvents events)
    {
        if (options.HostPublicKeySha256 is null && options.HostPublicKeyMd5 is null)
        {
            CheckKnownHosts(hostKey, host, port, knownHosts, events);
            return;
        }

        CheckSha256(hostKey, options.HostPublicKeySha256, events);
        CheckMd5(hostKey, options.HostPublicKeyMd5, events);
    }

    private static void CheckKnownHosts(byte[] hostKey, string host, int port, KnownHostsFile? knownHosts, ITransferEvents events)
    {
        if (knownHosts is null)
        {
            events.ReportInfo(SshInfoLines.NoKnownHostsFile);
            return;
        }

        if (KnownHostKeyTypeNames.FromName(SshKeyBlobReader.ReadKeyTypeName(hostKey)) == KnownHostKeyType.Unknown)
        {
            events.ReportInfo(SshInfoLines.UnsupportedHostKeyType);
            events.ReportInfo(SshInfoLines.KnownHostCheckFailed);
            throw SshTransferException.KnownHostRefused();
        }

        KnownHostsLookup lookup = knownHosts.Lookup(host, port, hostKey);
        events.ReportInfo(SshInfoLines.HostCheck((int)lookup.Check, lookup.Key));
        if (lookup.Check != KnownHostsCheck.Match)
        {
            events.ReportInfo(SshInfoLines.KnownHostCheckFailed);
            throw SshTransferException.KnownHostRefused();
        }

        events.ReportInfo(SshInfoLines.KnownHostMatches);
    }

    private static void CheckSha256(byte[] hostKey, string? expected, ITransferEvents events)
    {
        if (expected is null)
        {
            return;
        }

        string fingerprint = Convert.ToBase64String(SHA256.HashData(hostKey));
        events.ReportInfo(SshInfoLines.Sha256PublicKey(expected));
        events.ReportInfo(SshInfoLines.Sha256Fingerprint(fingerprint));
        if (expected.Split('=')[0] != fingerprint.Split('=')[0])
        {
            throw Denied("SHA256", fingerprint, expected);
        }

        events.ReportInfo(SshInfoLines.Sha256Match);
    }

    private static void CheckMd5(byte[] hostKey, string? expected, ITransferEvents events)
    {
        if (expected is null)
        {
            return;
        }

        string fingerprint = Convert.ToHexStringLower(MD5.HashData(hostKey));
        events.ReportInfo(SshInfoLines.Md5PublicKey(expected));
        events.ReportInfo(SshInfoLines.Md5Fingerprint(fingerprint));
        if (!string.Equals(fingerprint, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw Denied("MD5", fingerprint, expected);
        }

        events.ReportInfo(SshInfoLines.Md5Match);
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
