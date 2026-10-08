using Curl.Testing;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="SpnegoNegotiationResponse" /> to RFC 4178 section 4.2.2: each
/// <c>negState</c> an acceptor answers with, every field optional, and a typed failure for
/// anything that is not a NegTokenResp.
/// </summary>
[TestClass]
public sealed class SpnegoNegotiationResponseTests
{
    private const string AcceptCompletedKerberos =
        "A121301FA0030A0100A10B06092A864886F712010202A2050403AABBCCA3040402DDEE";

    private const string AcceptIncompleteNtlm =
        "A11D301BA0030A0101A10C060A2B06010401823702020AA20604044E544C4D";

    private const string Reject = "A1073005A0030A0102";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Decode_AcceptCompleted_ReadsEveryField()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("token hex", AcceptCompletedKerberos);
        diagnostics.Bytes("token", Convert.FromHexString(AcceptCompletedKerberos));

        SpnegoNegotiationResponse response = SpnegoNegotiationResponse.Decode(Convert.FromHexString(AcceptCompletedKerberos));

        diagnostics.Act("response", response);
        diagnostics.Assert("state", SpnegoNegotiationState.AcceptCompleted, response.State);
        diagnostics.Assert("supported mechanism", SpnegoMechanism.KerberosV5, response.SupportedMechanism);
        diagnostics.Diff("response token hex", "AABBCC", Convert.ToHexString(response.ResponseToken!.Value.Span));
        diagnostics.Diff("mechanism list MIC hex", "DDEE", Convert.ToHexString(response.MechanismListMic!.Value.Span));
        Assert.AreEqual(SpnegoNegotiationState.AcceptCompleted, response.State);
        Assert.AreEqual(SpnegoMechanism.KerberosV5, response.SupportedMechanism);
        Assert.AreEqual("AABBCC", Convert.ToHexString(response.ResponseToken!.Value.Span));
        Assert.AreEqual("DDEE", Convert.ToHexString(response.MechanismListMic!.Value.Span));
    }

    [TestMethod]
    public void Decode_AcceptIncomplete_ReadsTheNtlmChallenge()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("token hex", AcceptIncompleteNtlm);
        diagnostics.Bytes("token", Convert.FromHexString(AcceptIncompleteNtlm));

        SpnegoNegotiationResponse response = SpnegoNegotiationResponse.Decode(Convert.FromHexString(AcceptIncompleteNtlm));

        diagnostics.Act("response", response);
        diagnostics.Assert("state", SpnegoNegotiationState.AcceptIncomplete, response.State);
        diagnostics.Assert("supported mechanism", SpnegoMechanism.Ntlmssp, response.SupportedMechanism);
        diagnostics.Diff("response token text", "NTLM", System.Text.Encoding.ASCII.GetString(response.ResponseToken!.Value.Span));
        diagnostics.Assert("mechanism list MIC", null, response.MechanismListMic);
        Assert.AreEqual(SpnegoNegotiationState.AcceptIncomplete, response.State);
        Assert.AreEqual(SpnegoMechanism.Ntlmssp, response.SupportedMechanism);
        Assert.AreEqual("NTLM", System.Text.Encoding.ASCII.GetString(response.ResponseToken!.Value.Span));
        Assert.IsNull(response.MechanismListMic);
    }

    [TestMethod]
    public void Decode_Reject_HasOnlyTheState()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("token hex", Reject);
        diagnostics.Bytes("token", Convert.FromHexString(Reject));

        SpnegoNegotiationResponse response = SpnegoNegotiationResponse.Decode(Convert.FromHexString(Reject));

        diagnostics.Act("response", response);
        diagnostics.Assert("response", new SpnegoNegotiationResponse(SpnegoNegotiationState.Reject, null, null, null), response);
        Assert.AreEqual(new SpnegoNegotiationResponse(SpnegoNegotiationState.Reject, null, null, null), response);
    }

    [TestMethod]
    public void Decode_RequestMic_ReadsTheState()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("token hex", "A1073005A0030A0103");
        diagnostics.Bytes("token", Convert.FromHexString("A1073005A0030A0103"));

        SpnegoNegotiationResponse response = SpnegoNegotiationResponse.Decode(Convert.FromHexString("A1073005A0030A0103"));

        diagnostics.Act("response", response);
        diagnostics.Assert("state", SpnegoNegotiationState.RequestMic, response.State);
        Assert.AreEqual(SpnegoNegotiationState.RequestMic, response.State);
    }

    [TestMethod]
    public void Decode_NoFields_ReturnsAllAbsent()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("token hex", "A1023000");
        diagnostics.Bytes("token", Convert.FromHexString("A1023000"));

        SpnegoNegotiationResponse response = SpnegoNegotiationResponse.Decode(Convert.FromHexString("A1023000"));

        diagnostics.Act("response", response);
        diagnostics.Assert("response", new SpnegoNegotiationResponse(null, null, null, null), response);
        Assert.AreEqual(new SpnegoNegotiationResponse(null, null, null, null), response);
    }

    [TestMethod]
    [DataRow(AcceptCompletedKerberos, DisplayName = "Accept-completed, every field")]
    [DataRow(AcceptIncompleteNtlm, DisplayName = "Accept-incomplete, no MIC")]
    [DataRow(Reject, DisplayName = "Reject, state alone")]
    [DataRow("A1023000", DisplayName = "No fields")]
    [DataRow("A1073005A30304010F", DisplayName = "MIC alone")]
    public void Encode_DecodedResponse_ReturnsTheSameBytes(string hex)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("token hex", hex);
        diagnostics.Bytes("token", Convert.FromHexString(hex));

        SpnegoNegotiationResponse response = SpnegoNegotiationResponse.Decode(Convert.FromHexString(hex));

        diagnostics.Act("response", response);
        diagnostics.Diff("encoded hex", hex, Convert.ToHexString(response.Encode()));
        Assert.AreEqual(hex, Convert.ToHexString(response.Encode()));
    }

    [TestMethod]
    [DataRow("", DisplayName = "Empty")]
    [DataRow("A1053003A003", DisplayName = "Truncated")]
    [DataRow("A102300000", DisplayName = "Bytes after the token")]
    [DataRow("A10430000500", DisplayName = "Bytes after the SEQUENCE")]
    [DataRow("A1073005A0030A0104", DisplayName = "negState RFC 4178 does not define")]
    [DataRow("A1093007A0050A01000500", DisplayName = "Bytes after a field's value")]
    [DataRow("A10C300AA2030401AAA0030A0100", DisplayName = "Fields out of order")]
    [DataRow("A1073005A003040100", DisplayName = "negState of the wrong type")]
    [DataRow("A1073005A4030401AA", DisplayName = "A field RFC 4178 does not define")]
    [DataRow("8100", DisplayName = "Primitive [1]")]
    [DataRow("A1020400", DisplayName = "Not a SEQUENCE inside [1]")]
    public void Decode_Malformed_ThrowsMalformed(string hex)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("token hex", hex);
        diagnostics.Bytes("token", Convert.FromHexString(hex));

        SpnegoTokenException exception = Assert.ThrowsExactly<SpnegoTokenException>(
            () => SpnegoNegotiationResponse.Decode(Convert.FromHexString(hex)));

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("error", SpnegoTokenError.Malformed, exception.Error);
        diagnostics.Diff("message", "SPNEGO token could not be decoded: Malformed.", exception.Message);
        Assert.AreEqual(SpnegoTokenError.Malformed, exception.Error);
        Assert.AreEqual("SPNEGO token could not be decoded: Malformed.", exception.Message);
    }

    [TestMethod]
    [DataRow("A0023000", DisplayName = "A NegTokenInit choice")]
    [DataRow("602106062B0601050502A0173015A00E300C060A2B06010401823702020AA2030401AA", DisplayName = "A framed initial token")]
    public void Decode_AnotherToken_ThrowsUnexpectedToken(string hex)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("token hex", hex);
        diagnostics.Bytes("token", Convert.FromHexString(hex));

        SpnegoTokenException exception = Assert.ThrowsExactly<SpnegoTokenException>(
            () => SpnegoNegotiationResponse.Decode(Convert.FromHexString(hex)));

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("error", SpnegoTokenError.UnexpectedToken, exception.Error);
        Assert.AreEqual(SpnegoTokenError.UnexpectedToken, exception.Error);
    }
}
