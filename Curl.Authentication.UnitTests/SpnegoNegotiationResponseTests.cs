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

    [TestMethod]
    public void Decode_AcceptCompleted_ReadsEveryField()
    {
        SpnegoNegotiationResponse response = SpnegoNegotiationResponse.Decode(Convert.FromHexString(AcceptCompletedKerberos));

        Assert.AreEqual(SpnegoNegotiationState.AcceptCompleted, response.State);
        Assert.AreEqual(SpnegoMechanism.KerberosV5, response.SupportedMechanism);
        Assert.AreEqual("AABBCC", Convert.ToHexString(response.ResponseToken!.Value.Span));
        Assert.AreEqual("DDEE", Convert.ToHexString(response.MechanismListMic!.Value.Span));
    }

    [TestMethod]
    public void Decode_AcceptIncomplete_ReadsTheNtlmChallenge()
    {
        SpnegoNegotiationResponse response = SpnegoNegotiationResponse.Decode(Convert.FromHexString(AcceptIncompleteNtlm));

        Assert.AreEqual(SpnegoNegotiationState.AcceptIncomplete, response.State);
        Assert.AreEqual(SpnegoMechanism.Ntlmssp, response.SupportedMechanism);
        Assert.AreEqual("NTLM", System.Text.Encoding.ASCII.GetString(response.ResponseToken!.Value.Span));
        Assert.IsNull(response.MechanismListMic);
    }

    [TestMethod]
    public void Decode_Reject_HasOnlyTheState()
    {
        SpnegoNegotiationResponse response = SpnegoNegotiationResponse.Decode(Convert.FromHexString(Reject));

        Assert.AreEqual(new SpnegoNegotiationResponse(SpnegoNegotiationState.Reject, null, null, null), response);
    }

    [TestMethod]
    public void Decode_RequestMic_ReadsTheState()
    {
        SpnegoNegotiationResponse response = SpnegoNegotiationResponse.Decode(Convert.FromHexString("A1073005A0030A0103"));

        Assert.AreEqual(SpnegoNegotiationState.RequestMic, response.State);
    }

    [TestMethod]
    public void Decode_NoFields_ReturnsAllAbsent()
    {
        SpnegoNegotiationResponse response = SpnegoNegotiationResponse.Decode(Convert.FromHexString("A1023000"));

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
        SpnegoNegotiationResponse response = SpnegoNegotiationResponse.Decode(Convert.FromHexString(hex));

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
        SpnegoTokenException exception = Assert.ThrowsExactly<SpnegoTokenException>(
            () => SpnegoNegotiationResponse.Decode(Convert.FromHexString(hex)));

        Assert.AreEqual(SpnegoTokenError.Malformed, exception.Error);
        Assert.AreEqual("SPNEGO token could not be decoded: Malformed.", exception.Message);
    }

    [TestMethod]
    [DataRow("A0023000", DisplayName = "A NegTokenInit choice")]
    [DataRow("602106062B0601050502A0173015A00E300C060A2B06010401823702020AA2030401AA", DisplayName = "A framed initial token")]
    public void Decode_AnotherToken_ThrowsUnexpectedToken(string hex)
    {
        SpnegoTokenException exception = Assert.ThrowsExactly<SpnegoTokenException>(
            () => SpnegoNegotiationResponse.Decode(Convert.FromHexString(hex)));

        Assert.AreEqual(SpnegoTokenError.UnexpectedToken, exception.Error);
    }
}
