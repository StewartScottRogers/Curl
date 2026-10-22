using Curl.Networking;

namespace Curl.Conformance.UnitProgram.Dns.Tls;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit3304.c</c>: <c>Curl_ssl_peer_key_make</c>, here
/// <see cref="TlsSessionCache.PeerKey"/>, for <c>example.com:443</c> with a verified peer, the client
/// certificate <c>client.pem</c>, key <c>client.key</c>, pass phrase <c>secret</c> and both types
/// <c>PEM</c>. The C program's seven checks, in order: the same options give the same key; no pass phrase
/// gives the same key; key <c>other.key</c>, key type <c>DER</c> and certificate type <c>P12</c> each give
/// a different key; certificate type <c>pem</c> and key type <c>pem</c> each give the same key. Curl has
/// no <c>struct ssl_primary_config</c>: the options are a <see cref="TlsClientOptions"/>, and only the
/// observable part, whether the two keys are equal, is compared. Each failing check writes the C
/// program's message to standard error and counts once.
/// </summary>
/// <param name="peerKey">Makes the peer key of one set of options; <see cref="TlsSessionCache.PeerKey"/> for <c>example.com:443</c> unless a test injects another.</param>
public sealed class Unit3304TlsSessionPeerKey(Func<TlsClientOptions, string> peerKey) : IUnitProgram
{
    private static readonly TlsClientOptions Base = new(
        ClientCertificate: "client.pem",
        PrivateKey: "client.key",
        CertificateType: "PEM",
        PrivateKeyType: "PEM",
        Passphrase: "secret");

    private static readonly (TlsClientOptions Changed, bool SameKey, string Message)[] Checks =
    [
        (Base, true, "identical config should produce identical peer key"),
        (Base with { Passphrase = null }, true, "key_passwd must not affect the peer key"),
        (Base with { PrivateKey = "other.key" }, false, "different key must produce different peer key"),
        (Base with { PrivateKeyType = "DER" }, false, "different key_type must produce different peer key"),
        (Base with { CertificateType = "P12" }, false, "different cert_type must produce different peer key"),
        (Base with { CertificateType = "pem" }, true, "cert_type case must not affect peer key"),
        (Base with { PrivateKeyType = "pem" }, true, "key_type case must not affect peer key"),
    ];

    /// <summary>Creates the port over <see cref="TlsSessionCache.PeerKey"/>.</summary>
    public Unit3304TlsSessionPeerKey()
        : this(options => TlsSessionCache.PeerKey("example.com", 443, options))
    {
    }

    /// <inheritdoc />
    public string ProgramName => "unit3304";

    /// <inheritdoc />
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int failures = 0;
        foreach ((TlsClientOptions changed, bool sameKey, string message) in Checks)
        {
            if ((peerKey(Base) == peerKey(changed)) != sameKey)
            {
                await context.StandardError.WriteAsync($"unit3304.c Assertion FAILED: {message}\n").ConfigureAwait(false);
                failures++;
            }
        }

        return new UnitProgramResult(failures);
    }
}
