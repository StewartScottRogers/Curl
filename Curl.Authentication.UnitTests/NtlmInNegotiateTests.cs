using System.Formats.Asn1;
using Curl.Testing;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="NtlmInNegotiate" /> to the two shapes SSPI's Negotiate fell back to NTLM in
/// on 2026-09-28 (BL-527 Notes) - a bare NTLMSSP Type 1 for <c>HTTP/127.0.0.1</c> and a
/// NegTokenInit carrying one for <c>HTTP/localhost</c> - and to Kerberos tokens it must pass.
/// </summary>
[TestClass]
public sealed class NtlmInNegotiateTests
{
    // SSPI's Negotiate first token for HTTP/localhost off a domain: NEGOEX, MS-KRB5, KRB5 and
    // NTLM offered, with an NTLM Type 1 as the optimistic token.
    private const string MeasuredSpnegoWithNtlm =
        "YIGCBgYrBgEFBQKgeDB2oDAwLgYKKwYBBAGCNwICCgYJKoZIgvcSAQICBgkqhkiG9xIBAgIGCisGAQQBgjcCAh6iQgRATlRMTVNTUAABAAAAl7II4gkACQA3AAAADwAPACgAAAAKAPRlAAAAD1NURVdBUlQtUk9HRVJTLVdPUktHUk9VUA==";

    // SSPI's Negotiate first token for HTTP/127.0.0.1 off a domain: a bare NTLM Type 1.
    private const string MeasuredBareNtlm =
        "TlRMTVNTUAABAAAAl7II4gkACQA3AAAADwAPACgAAAAKAPRlAAAAD1NURVdBUlQtUk9HRVJTLVdPUktHUk9VUA==";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(MeasuredSpnegoWithNtlm, DisplayName = "NegTokenInit carrying NTLM")]
    [DataRow(MeasuredBareNtlm, DisplayName = "Bare NTLM")]
    public void Carries_MeasuredNtlmFallback_IsTrue(string base64)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] token = Convert.FromBase64String(base64);
        diagnostics.Arrange("token base64", base64);
        diagnostics.Bytes("token", token);

        bool carries = NtlmInNegotiate.Carries(token);

        diagnostics.Act("carries NTLM", carries);
        diagnostics.Assert("carries NTLM", true, carries);
        Assert.IsTrue(NtlmInNegotiate.Carries(Convert.FromBase64String(base64)));
    }

    [TestMethod]
    public void Carries_NegTokenInitCarryingKerberos_IsFalse()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism types", "MIT Kerberos");
        byte[] token = SpnegoInitialToken.Encode(SpnegoMechanism.MitKerberosMechanismTypes, [0x60, 0x01, 0x00]);
        diagnostics.Bytes("token", token);

        bool carries = NtlmInNegotiate.Carries(token);

        diagnostics.Act("carries NTLM", carries);
        diagnostics.Assert("carries NTLM", false, carries);
        Assert.IsFalse(NtlmInNegotiate.Carries(SpnegoInitialToken.Encode(SpnegoMechanism.MitKerberosMechanismTypes, [0x60, 0x01, 0x00])));
    }

    [TestMethod]
    public void Carries_BareKerberosGssToken_IsFalse()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("token shape", "application 0 sequence with the Kerberos V5 object identifier");
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence(new Asn1Tag(TagClass.Application, 0, isConstructed: true)))
        {
            writer.WriteObjectIdentifier(SpnegoMechanism.KerberosV5);
        }

        byte[] token = writer.Encode();
        diagnostics.Bytes("token", token);
        bool carries = NtlmInNegotiate.Carries(token);

        diagnostics.Act("carries NTLM", carries);
        diagnostics.Assert("carries NTLM", false, carries);
        Assert.IsFalse(NtlmInNegotiate.Carries(writer.Encode()));
    }

    [TestMethod]
    public void Carries_NegTokenInitWithoutMechToken_IsFalse()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("token shape", "NegTokenInit offering NTLMSSP with no mechanism token");
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence(new Asn1Tag(TagClass.Application, 0, isConstructed: true)))
        {
            writer.WriteObjectIdentifier(SpnegoMechanism.Spnego);
            using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true)))
            using (writer.PushSequence())
            using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true)))
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(SpnegoMechanism.Ntlmssp);
            }
        }

        byte[] token = writer.Encode();
        diagnostics.Bytes("token", token);
        bool carries = NtlmInNegotiate.Carries(token);

        diagnostics.Act("carries NTLM", carries);
        diagnostics.Assert("carries NTLM", false, carries);
        Assert.IsFalse(NtlmInNegotiate.Carries(writer.Encode()));
    }

    [TestMethod]
    [DataRow(new byte[0], DisplayName = "Empty")]
    [DataRow(new byte[] { 0x60, 0x05, 0x01 }, DisplayName = "Truncated")]
    public void Carries_NotAToken_IsFalse(byte[] token)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("token length", token.Length);
        diagnostics.Bytes("token", token);

        bool carries = NtlmInNegotiate.Carries(token);

        diagnostics.Act("carries NTLM", carries);
        diagnostics.Assert("carries NTLM", false, carries);
        Assert.IsFalse(NtlmInNegotiate.Carries(token));
    }
}
