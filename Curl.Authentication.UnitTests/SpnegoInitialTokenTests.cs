using Curl.Testing;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="SpnegoInitialToken" /> to the NegTokenInit curl 8.18.0 with MIT krb5
/// 1.22.1 sent on 2026-09-28, byte for byte, and to RFC 4178's layout for longer
/// mechanism lists.
/// </summary>
[TestClass]
public sealed class SpnegoInitialTokenTests
{
    // The Authorization: Negotiate token curl sent to http://127.0.0.1:18692/ from a
    // credential cache holding a service ticket for HTTP/localhost@TEST.ORG (BL-692 Notes).
    private const string MeasuredMitToken =
        "YIIBpwYGKwYBBQUCoIIBmzCCAZegDTALBgkqhkiG9xIBAgKiggGEBIIBgGCCAXwGCSqGSIb3EgECAgEAboIBazCCAWegAwIBBaEDAgEOogcDBQAgAAAAo4GHYYGEMIGBoAMCAQWhChsIVEVTVC5PUkeiHDAaoAMCAQOhEzARGwRIVFRQGwlsb2NhbGhvc3SjUDBOoAMCARKhAwIBAaJCBEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAApIHHMIHEoAMCARKigbwEgbl11iRFt/nyxx5DWYBenynZq+UP8yMG1p1LWE1m4GlKoHo6q7lg8Vyw6sD16g9W2XQ9xHik/hsh6/Z3YW7z+X/30b1fteHCDJx4pvd9akGBOtNaU0QmDIYqffgs4T8iAJskRFNIFofxrtiKWVuGkn8bqPvKwrb7SxmCd56CaFsJLlibVls9puDHK5Mdifot53QWqJiQY0+LRtGDVHTIXXpFuXvfH9toQ4MP1a6NrmmKS1eIUF97JsFskA==";

    // Where the Kerberos AP-REQ starts inside the measured token: after the framing,
    // thisMech, the [0] choice, the SEQUENCE, mechTypes and the mechToken headers.
    private const int MeasuredMechanismTokenOffset = 43;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Encode_MitKerberosListAndMeasuredApRequest_ReturnsCurlsBytes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] measured = Convert.FromBase64String(MeasuredMitToken);
        diagnostics.Arrange("mechanism token offset", MeasuredMechanismTokenOffset);
        diagnostics.Bytes("measured token", measured);

        byte[] encoded = SpnegoInitialToken.Encode(
            SpnegoMechanism.MitKerberosMechanismTypes,
            measured.AsSpan(MeasuredMechanismTokenOffset));

        diagnostics.Bytes("encoded", encoded);
        diagnostics.Act("encoded length", encoded.Length);
        diagnostics.Diff("encoded base64", MeasuredMitToken, Convert.ToBase64String(encoded));
        Assert.AreEqual(MeasuredMitToken, Convert.ToBase64String(encoded));
    }

    [TestMethod]
    public void Encode_KerberosMicrosoftKerberosAndNtlmssp_OffersThemInOrder()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanisms", "KerberosV5, MicrosoftKerberosV5, Ntlmssp");
        diagnostics.Bytes("mechanism token", new byte[] { 0x01, 0x02, 0x03 });

        byte[] encoded = SpnegoInitialToken.Encode(
            [SpnegoMechanism.KerberosV5, SpnegoMechanism.MicrosoftKerberosV5, SpnegoMechanism.Ntlmssp],
            [0x01, 0x02, 0x03]);

        diagnostics.Bytes("encoded", encoded);
        diagnostics.Act("encoded length", encoded.Length);
        diagnostics.Diff(
            "encoded hex",
            "603906062B0601050502A02F302DA0243022"
                + "06092A864886F71201020206092A864882F712010202060A2B06010401823702020A"
                + "A2050403010203",
            Convert.ToHexString(encoded));
        Assert.AreEqual(
            "603906062B0601050502A02F302DA0243022"
                + "06092A864886F71201020206092A864882F712010202060A2B06010401823702020A"
                + "A2050403010203",
            Convert.ToHexString(encoded));
    }

    [TestMethod]
    public void Encode_NtlmsspAlone_WrapsTheNtlmToken()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanisms", "Ntlmssp");
        diagnostics.Bytes("mechanism token", new byte[] { 0xAA });

        byte[] encoded = SpnegoInitialToken.Encode([SpnegoMechanism.Ntlmssp], [0xAA]);

        diagnostics.Bytes("encoded", encoded);
        diagnostics.Act("encoded length", encoded.Length);
        diagnostics.Diff(
            "encoded hex",
            "602106062B0601050502A0173015A00E300C060A2B06010401823702020AA2030401AA",
            Convert.ToHexString(encoded));
        Assert.AreEqual(
            "602106062B0601050502A0173015A00E300C060A2B06010401823702020AA2030401AA",
            Convert.ToHexString(encoded));
    }
}
