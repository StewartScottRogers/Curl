using Curl.Extension.Dane.Wire;

namespace Curl.Extension.Dane.Dnssec;

/// <summary>
/// The root zone's trust anchors as DS records, from IANA's root-anchors.xml: KSK-2017 (key tag 20326) and KSK-2024
/// (key tag 38696), both algorithm 8 RSA/SHA-256 with a SHA-256 digest. Pass them to
/// <see cref="DelegationVerifier.Verify" /> with a root DNSKEY.
/// </summary>
public static class RootTrustAnchors
{
    /// <summary>Gets the root key-signing keys' DS records.</summary>
    public static IReadOnlyList<DsRecordData> DelegationSigners { get; } =
    [
        new DsRecordData(20326, 8, 2, Convert.FromHexString("E06D44B80B8F1D39A95C0B0D7C65D08458E880409BBC683457104237C7F8EC8D")),
        new DsRecordData(38696, 8, 2, Convert.FromHexString("683D2D0ACB8C9B712A1948B27F741219298D0A450D612C483AF444A4C0FB2B16")),
    ];
}
