using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh.Negotiation;

/// <summary>
/// The algorithm lists a client <c>KEXINIT</c> offers, in order of preference, the same in
/// both directions (ADR-0122). The composition injects <see cref="WindowsReference" /> on
/// Windows and <see cref="OpenSslReference" /> elsewhere; <see cref="Full" /> lists every
/// algorithm either SSH backend of curl offers.
/// </summary>
/// <param name="KeyExchange">The key-exchange methods, ending with the <c>ext-info-c</c> and strict key exchange signals.</param>
/// <param name="ServerHostKey">The host-key algorithms.</param>
/// <param name="Cipher">The ciphers.</param>
/// <param name="Mac">The MACs.</param>
/// <param name="Compression">The compression methods.</param>
public sealed record SshAlgorithmPreferences(
    IReadOnlyList<string> KeyExchange,
    IReadOnlyList<string> ServerHostKey,
    IReadOnlyList<string> Cipher,
    IReadOnlyList<string> Mac,
    IReadOnlyList<string> Compression)
{
    private const string SignalNames = ",ext-info-c,kex-strict-c-v00@openssh.com";

    private const string FiniteFieldKeyExchanges =
        "diffie-hellman-group-exchange-sha256,diffie-hellman-group16-sha512,diffie-hellman-group18-sha512,"
        + "diffie-hellman-group14-sha256,diffie-hellman-group14-sha1,diffie-hellman-group1-sha1,"
        + "diffie-hellman-group-exchange-sha1";

    private const string EllipticCurveKeyExchanges =
        "curve25519-sha256,curve25519-sha256@libssh.org,ecdh-sha2-nistp256,ecdh-sha2-nistp384,ecdh-sha2-nistp521,";

    private const string RsaHostKeys =
        "rsa-sha2-512,rsa-sha2-256,rsa-sha2-512-cert-v01@openssh.com,rsa-sha2-256-cert-v01@openssh.com,"
        + "ssh-rsa,ssh-rsa-cert-v01@openssh.com";

    private const string RsaCertificateHostKeys =
        "rsa-sha2-512-cert-v01@openssh.com,rsa-sha2-256-cert-v01@openssh.com,ssh-rsa-cert-v01@openssh.com";

    private const string EcdsaCertificateHostKeys =
        "ecdsa-sha2-nistp256-cert-v01@openssh.com,ecdsa-sha2-nistp384-cert-v01@openssh.com,ecdsa-sha2-nistp521-cert-v01@openssh.com";

    private const string OpenSslCiphers =
        "chacha20-poly1305@openssh.com,aes256-gcm@openssh.com,aes128-gcm@openssh.com,aes256-ctr,aes192-ctr,"
        + "aes128-ctr,aes256-cbc,rijndael-cbc@lysator.liu.se,aes192-cbc,aes128-cbc,blowfish-cbc,arcfour128,"
        + "arcfour,cast128-cbc,3des-cbc";

    private const string WindowsMacs =
        "hmac-sha2-256,hmac-sha2-256-etm@openssh.com,hmac-sha2-512,hmac-sha2-512-etm@openssh.com,hmac-sha1,"
        + "hmac-sha1-etm@openssh.com,hmac-sha1-96,hmac-md5,hmac-md5-96";

    private const string OpenSslMacs = WindowsMacs + ",hmac-ripemd160,hmac-ripemd160@openssh.com";

    private static readonly Dictionary<string, string[]> KnownHostNarrowing = new(StringComparer.Ordinal)
    {
        ["ssh-rsa"] = ["rsa-sha2-256", "rsa-sha2-512", "ssh-rsa"],
        ["ssh-ed25519"] = ["ssh-ed25519"],
        ["ecdsa-sha2-nistp256"] = ["ecdsa-sha2-nistp256"],
        ["ecdsa-sha2-nistp384"] = ["ecdsa-sha2-nistp384"],
        ["ecdsa-sha2-nistp521"] = ["ecdsa-sha2-nistp521"],
        ["ssh-dss"] = ["ssh-dss"],
    };

    /// <summary>
    /// Gets the libssh2 cryptography backend the platform's curl names in its <c>-v</c> line
    /// <c>SSH: libssh2 cryptography backend: &lt;name&gt;</c>: <c>WinCNG</c> for
    /// <see cref="WindowsReference" />, <c>OpenSSL</c> for <see cref="OpenSslReference" />,
    /// and <see langword="null" />, writing no line, otherwise (ADR-0262).
    /// </summary>
    public string? CryptographyBackend { get; init; }

    /// <summary>
    /// Gets the host-key algorithms this preset offers but never agrees: libssh2 1.11.1 lists
    /// the RSA and ECDSA certificate forms in its <c>KEXINIT</c> but has no verifier for them,
    /// so it passes over them when it picks the host-key algorithm and fails with
    /// <c>-5, Unable to exchange encryption keys</c> when nothing else is shared (measured
    /// 2026-09-29 on both reference builds, ADR-0266). Empty for <see cref="Full" />.
    /// </summary>
    public IReadOnlyCollection<string> HostKeysNeverAgreed { get; init; } = [];

    /// <summary>
    /// Gets what curl 8.21.0's Windows build (libssh2 1.11.1 on WinCNG) offers, byte for byte
    /// and in its order.
    /// </summary>
    public static SshAlgorithmPreferences WindowsReference { get; } = new(
        Split(FiniteFieldKeyExchanges + SignalNames),
        Split(RsaHostKeys),
        Split(
            "chacha20-poly1305@openssh.com,aes256-ctr,aes192-ctr,aes128-ctr,aes256-cbc,rijndael-cbc@lysator.liu.se,"
            + "aes192-cbc,aes128-cbc,arcfour128,arcfour,3des-cbc"),
        Split(WindowsMacs),
        [SshAlgorithmCatalogue.NoCompression])
    { CryptographyBackend = "WinCNG", HostKeysNeverAgreed = Split(RsaCertificateHostKeys) };

    /// <summary>
    /// Gets what curl 8.21.0's OpenSSL build (libssh2 1.11.1 on OpenSSL, Linux and macOS)
    /// offers, byte for byte and in its order.
    /// </summary>
    public static SshAlgorithmPreferences OpenSslReference { get; } = new(
        Split(EllipticCurveKeyExchanges + FiniteFieldKeyExchanges + SignalNames),
        Split(
            "ecdsa-sha2-nistp256,ecdsa-sha2-nistp384,ecdsa-sha2-nistp521,ecdsa-sha2-nistp256-cert-v01@openssh.com,"
            + "ecdsa-sha2-nistp384-cert-v01@openssh.com,ecdsa-sha2-nistp521-cert-v01@openssh.com,ssh-ed25519,"
            + "ssh-ed25519-cert-v01@openssh.com," + RsaHostKeys),
        Split(OpenSslCiphers),
        Split(OpenSslMacs),
        [SshAlgorithmCatalogue.NoCompression])
    { CryptographyBackend = "OpenSSL", HostKeysNeverAgreed = Split(EcdsaCertificateHostKeys + "," + RsaCertificateHostKeys) };

    /// <summary>
    /// Gets every algorithm libssh2 1.11.1 or libssh 0.12.2 offers, in ADR-0122's full-set
    /// order. Nothing in the console selects it.
    /// </summary>
    public static SshAlgorithmPreferences Full { get; } = new(
        Split(
            "mlkem768x25519-sha256,mlkem768nistp256-sha256,mlkem1024nistp384-sha384,sntrup761x25519-sha512,"
            + "sntrup761x25519-sha512@openssh.com," + EllipticCurveKeyExchanges + FiniteFieldKeyExchanges + SignalNames),
        Split(
            "ecdsa-sha2-nistp256,ecdsa-sha2-nistp384,ecdsa-sha2-nistp521,ecdsa-sha2-nistp256-cert-v01@openssh.com,"
            + "ecdsa-sha2-nistp384-cert-v01@openssh.com,sk-ssh-ed25519-cert-v01@openssh.com,"
            + "ecdsa-sha2-nistp521-cert-v01@openssh.com,ssh-ed25519,ssh-ed25519-cert-v01@openssh.com,"
            + "sk-ssh-ed25519@openssh.com,sk-ecdsa-sha2-nistp256@openssh.com,rsa-sha2-512,rsa-sha2-256,"
            + "sk-ecdsa-sha2-nistp256-cert-v01@openssh.com,rsa-sha2-512-cert-v01@openssh.com,"
            + "rsa-sha2-256-cert-v01@openssh.com,ssh-rsa,ssh-rsa-cert-v01@openssh.com,ssh-dss"),
        Split(OpenSslCiphers),
        Split(OpenSslMacs + ",hmac-md5-etm@openssh.com"),
        [SshAlgorithmCatalogue.NoCompression]);

    /// <summary>
    /// Gets these preferences with the compression list curl offers for
    /// <c>--compressed-ssh</c>: <c>zlib,zlib@openssh.com,none</c>.
    /// </summary>
    /// <param name="compression">Whether <c>--compressed-ssh</c> was given.</param>
    /// <returns>These preferences, with compression offered when asked for.</returns>
    internal SshAlgorithmPreferences WithCompression(bool compression) =>
        compression ? this with { Compression = ["zlib", "zlib@openssh.com", SshAlgorithmCatalogue.NoCompression] } : this;

    /// <summary>
    /// Narrows the host-key list to the type of the known-hosts entry that matches the host,
    /// as curl 8.21.0's <c>lib/vssh/libssh2.c</c> does before the handshake: <c>ssh-rsa</c>
    /// to <c>rsa-sha2-256,rsa-sha2-512,ssh-rsa</c>, every other known type to itself, and an
    /// unknown type to no change.
    /// </summary>
    /// <param name="knownHostKeyType">The key type of the matching known-hosts entry.</param>
    /// <returns>These preferences with the narrowed host-key list.</returns>
    /// <exception cref="SshTransferException">
    /// None of the narrowed names is in this preset: exit 79, <c>libssh2 method '&lt;names&gt;'
    /// failed: The requested method(s) are not currently supported</c>, as the WinCNG build
    /// fails for an <c>ssh-ed25519</c> entry.
    /// </exception>
    internal SshAlgorithmPreferences NarrowHostKeysTo(string knownHostKeyType)
    {
        if (!KnownHostNarrowing.TryGetValue(knownHostKeyType, out string[]? narrowed))
        {
            return this;
        }

        List<string> offered = [.. narrowed.Where(ServerHostKey.Contains)];
        return offered.Count > 0
            ? this with { ServerHostKey = offered }
            : throw new SshTransferException(
                CurlExitCode.Ssh,
                $"libssh2 method '{string.Join(',', narrowed)}' failed: The requested method(s) are not currently supported");
    }

    /// <summary>
    /// Gets the host-key algorithms curl narrows a known-hosts entry's type to, as its
    /// <c>SSH: set '&lt;names&gt;' as hostkey type</c> line names them (ADR-0262).
    /// </summary>
    /// <param name="knownHostKeyType">A key type <see cref="NarrowHostKeysTo" /> recognizes, such as <c>ssh-rsa</c>.</param>
    /// <returns>The names, comma-separated, such as <c>rsa-sha2-256,rsa-sha2-512,ssh-rsa</c>.</returns>
    internal static string NarrowedHostKeyNames(string knownHostKeyType) =>
        string.Join(',', KnownHostNarrowing[knownHostKeyType]);

    private static string[] Split(string names) => names.Split(',');
}
