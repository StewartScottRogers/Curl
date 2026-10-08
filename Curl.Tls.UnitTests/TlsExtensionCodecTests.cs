using Curl.Testing;

namespace Curl.Tls;

/// <summary>
/// Round-trips every extension codec through its encoder and decoder, and checks each
/// decoder answers a truncated length or trailing bytes with <c>decode_error</c> and a
/// value the extension forbids with <c>illegal_parameter</c>.
/// </summary>
[TestClass]
public sealed class TlsExtensionCodecTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ServerNameRoundTripsAHostName()
    {
        Diagnostics.Arrange("host name", "example.com");

        TlsExtension extension = ServerNameExtension.EncodeHostName("example.com");
        Diagnostics.Bytes("encoded", extension.Data);
        Diagnostics.Act("decoded", ServerNameExtension.DecodeHostName(extension.Data).Value);

        Assert.AreEqual(TlsExtensionType.ServerName, extension.Type);
        Assert.AreEqual("example.com", ServerNameExtension.DecodeHostName(extension.Data).Value);
        AssertRejectsTruncationAndTrailingBytes(extension.Data, ServerNameExtension.DecodeHostName);
    }

    [TestMethod]
    public void ServerNameRejectsANameTypeOtherThanHostName()
    {
        byte[] data = ServerNameExtension.EncodeHostName("a").Data;
        data[2] = 1;
        Diagnostics.Arrange("name type", 1);
        Diagnostics.Bytes("server_name data with name type 1", data);

        TlsAlertDescription? alert = ServerNameExtension.DecodeHostName(data).Alert;
        Diagnostics.Act("alert", alert);

        Diagnostics.Assert("alert", TlsAlertDescription.IllegalParameter, alert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, alert);
    }

    [TestMethod]
    public void ServerNameRejectsASecondEntryInTheList()
    {
        byte[] data = Convert.FromHexString("000800000161" + "00000162");
        Diagnostics.Arrange("entries", "host names a and b");
        Diagnostics.Bytes("server_name data with two entries", data);

        TlsAlertDescription? alert = ServerNameExtension.DecodeHostName(data).Alert;
        Diagnostics.Act("alert", alert);

        Diagnostics.Assert("alert", TlsAlertDescription.DecodeError, alert);
        Assert.AreEqual(TlsAlertDescription.DecodeError, alert);
    }

    [TestMethod]
    public void ServerNameAcknowledgementIsEmptyAndAnythingElseIsADecodeError()
    {
        Diagnostics.Arrange("extension", "server_name acknowledgement");

        TlsExtension extension = ServerNameExtension.EncodeAcknowledgement();
        Diagnostics.Act("data length", extension.Data.Length);

        Diagnostics.Assert("alert for one byte", TlsAlertDescription.DecodeError, ServerNameExtension.DecodeAcknowledgement([0]));
        Assert.AreEqual(TlsExtensionType.ServerName, extension.Type);
        Assert.IsEmpty(extension.Data);
        Assert.IsNull(ServerNameExtension.DecodeAcknowledgement(extension.Data));
        Assert.AreEqual(TlsAlertDescription.DecodeError, ServerNameExtension.DecodeAcknowledgement([0]));
    }

    [TestMethod]
    public void SupportedGroupsRoundTrips()
    {
        Diagnostics.Arrange("groups", "0x11ec, 0x001d, 0x0017");

        TlsExtension extension = SupportedGroupsExtension.Encode([0x11ec, 0x001d, 0x0017]);
        Diagnostics.Bytes("encoded", extension.Data);
        Diagnostics.Act("decoded count", SupportedGroupsExtension.Decode(extension.Data).Value.Count);

        Assert.AreEqual(TlsExtensionType.SupportedGroups, extension.Type);
        CollectionAssert.AreEqual(new ushort[] { 0x11ec, 0x001d, 0x0017 }, SupportedGroupsExtension.Decode(extension.Data).Value.ToArray());
        AssertRejectsTruncationAndTrailingBytes(extension.Data, SupportedGroupsExtension.Decode);
    }

    [TestMethod]
    public void SupportedGroupsRejectsAnOddLengthList()
    {
        Diagnostics.Arrange("data", "0003001d00, a three-byte list");

        TlsAlertDescription? alert = SupportedGroupsExtension.Decode(Convert.FromHexString("0003001d00")).Alert;
        Diagnostics.Act("alert", alert);

        Diagnostics.Assert("alert", TlsAlertDescription.DecodeError, alert);
        Assert.AreEqual(TlsAlertDescription.DecodeError, alert);
    }

    [TestMethod]
    public void SignatureAlgorithmsRoundTripsInBothExtensions()
    {
        Diagnostics.Arrange("schemes", "signature_algorithms 0x0804, 0x0403; signature_algorithms_cert 0x0807");

        TlsExtension algorithms = SignatureAlgorithmsExtension.Encode([0x0804, 0x0403]);
        TlsExtension certificateAlgorithms = SignatureAlgorithmsExtension.EncodeCertificateSchemes([0x0807]);
        Diagnostics.Act("types", $"{algorithms.Type}, {certificateAlgorithms.Type}");

        Assert.AreEqual(TlsExtensionType.SignatureAlgorithms, algorithms.Type);
        Assert.AreEqual(TlsExtensionType.SignatureAlgorithmsCert, certificateAlgorithms.Type);
        CollectionAssert.AreEqual(new ushort[] { 0x0804, 0x0403 }, SignatureAlgorithmsExtension.Decode(algorithms.Data).Value.ToArray());
        CollectionAssert.AreEqual(new ushort[] { 0x0807 }, SignatureAlgorithmsExtension.Decode(certificateAlgorithms.Data).Value.ToArray());
        AssertRejectsTruncationAndTrailingBytes(algorithms.Data, SignatureAlgorithmsExtension.Decode);
    }

    [TestMethod]
    public void KeyShareRoundTripsTheClientShares()
    {
        TlsExtension extension = KeyShareExtension.EncodeClientShares(
            [new KeyShareEntry(0x001d, [1, 2, 3]), new KeyShareEntry(0x0017, [4])]);
        Diagnostics.Arrange("shares", "0x001d 010203, 0x0017 04");
        Diagnostics.Bytes("encoded client shares", extension.Data);

        IReadOnlyList<KeyShareEntry> shares = KeyShareExtension.DecodeClientShares(extension.Data).Value;
        Diagnostics.Act("decoded share count", shares.Count);

        Assert.AreEqual(TlsExtensionType.KeyShare, extension.Type);
        Assert.HasCount(2, shares);
        Assert.AreEqual(0x001d, shares[0].Group);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, shares[0].KeyExchange);
        Assert.AreEqual(0x0017, shares[1].Group);
        CollectionAssert.AreEqual(new byte[] { 4 }, shares[1].KeyExchange);
        AssertRejectsTruncationAndTrailingBytes(extension.Data, KeyShareExtension.DecodeClientShares);
    }

    [TestMethod]
    public void KeyShareRoundTripsTheServerShare()
    {
        TlsExtension extension = KeyShareExtension.EncodeServerShare(new KeyShareEntry(0x001d, [9, 8]));
        Diagnostics.Arrange("share", "0x001d 0908");
        Diagnostics.Bytes("encoded server share", extension.Data);

        KeyShareEntry share = KeyShareExtension.DecodeServerShare(extension.Data).Value;
        Diagnostics.Act("decoded group", $"0x{share.Group:x4}");

        Assert.AreEqual(0x001d, share.Group);
        CollectionAssert.AreEqual(new byte[] { 9, 8 }, share.KeyExchange);
        AssertRejectsTruncationAndTrailingBytes(extension.Data, KeyShareExtension.DecodeServerShare);
    }

    [TestMethod]
    public void KeyShareRoundTripsTheSelectedGroup()
    {
        Diagnostics.Arrange("selected group", "0x0017");

        TlsExtension extension = KeyShareExtension.EncodeSelectedGroup(0x0017);
        Diagnostics.Act("decoded", KeyShareExtension.DecodeSelectedGroup(extension.Data).Value);

        Assert.AreEqual(0x0017, KeyShareExtension.DecodeSelectedGroup(extension.Data).Value);
        AssertRejectsTruncationAndTrailingBytes(extension.Data, KeyShareExtension.DecodeSelectedGroup);
    }

    [TestMethod]
    public void SupportedVersionsRoundTripsTheOfferedAndSelectedForms()
    {
        Diagnostics.Arrange("versions", "offered 0x0304, 0x0303; selected 0x0304");

        TlsExtension offered = SupportedVersionsExtension.EncodeOffered([0x0304, 0x0303]);
        TlsExtension selected = SupportedVersionsExtension.EncodeSelected(0x0304);
        Diagnostics.Act("offered data", Convert.ToHexStringLower(offered.Data));

        Assert.AreEqual("0403040303", Convert.ToHexStringLower(offered.Data));
        CollectionAssert.AreEqual(new ushort[] { 0x0304, 0x0303 }, SupportedVersionsExtension.DecodeOffered(offered.Data).Value.ToArray());
        Assert.AreEqual(0x0304, SupportedVersionsExtension.DecodeSelected(selected.Data).Value);
        AssertRejectsTruncationAndTrailingBytes(offered.Data, SupportedVersionsExtension.DecodeOffered);
        AssertRejectsTruncationAndTrailingBytes(selected.Data, SupportedVersionsExtension.DecodeSelected);
    }

    [TestMethod]
    public void PskKeyExchangeModesRoundTrips()
    {
        Diagnostics.Arrange("modes", "1, 0");

        TlsExtension extension = PskKeyExchangeModesExtension.Encode([1, 0]);
        Diagnostics.Bytes("encoded", extension.Data);
        Diagnostics.Act("type", extension.Type);

        Assert.AreEqual(TlsExtensionType.PskKeyExchangeModes, extension.Type);
        CollectionAssert.AreEqual(new byte[] { 1, 0 }, PskKeyExchangeModesExtension.Decode(extension.Data).Value);
        AssertRejectsTruncationAndTrailingBytes(extension.Data, PskKeyExchangeModesExtension.Decode);
    }

    [TestMethod]
    public void PreSharedKeyRoundTripsTheOfferAndTheSelection()
    {
        OfferedPsks offer = new([new PskIdentity([1, 2], 0x01020304), new PskIdentity([3], 7)], [[0xaa, 0xbb], [0xcc]]);
        Diagnostics.Arrange("offer", "identities 0102 (age 0x01020304) and 03 (age 7), binders aabb and cc; selected 1");

        TlsExtension offered = PreSharedKeyExtension.EncodeOffered(offer);
        OfferedPsks decoded = PreSharedKeyExtension.DecodeOffered(offered.Data).Value;
        TlsExtension selected = PreSharedKeyExtension.EncodeSelected(1);
        Diagnostics.Bytes("encoded offer", offered.Data);
        Diagnostics.Act("decoded identities", decoded.Identities.Count);

        Assert.AreEqual(TlsExtensionType.PreSharedKey, offered.Type);
        Assert.HasCount(2, decoded.Identities);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, decoded.Identities[0].Identity);
        Assert.AreEqual(0x01020304u, decoded.Identities[0].ObfuscatedTicketAge);
        CollectionAssert.AreEqual(new byte[] { 3 }, decoded.Identities[1].Identity);
        Assert.AreEqual(7u, decoded.Identities[1].ObfuscatedTicketAge);
        CollectionAssert.AreEqual(new byte[] { 0xaa, 0xbb }, decoded.Binders[0]);
        CollectionAssert.AreEqual(new byte[] { 0xcc }, decoded.Binders[1]);
        Assert.AreEqual(1, PreSharedKeyExtension.DecodeSelected(selected.Data).Value);
        AssertRejectsTruncationAndTrailingBytes(offered.Data, PreSharedKeyExtension.DecodeOffered);
        AssertRejectsTruncationAndTrailingBytes(selected.Data, PreSharedKeyExtension.DecodeSelected);
    }

    [TestMethod]
    public void EarlyDataRoundTripsTheIndicationAndTheMaximumSize()
    {
        Diagnostics.Arrange("forms", "indication, max_early_data_size 16384");

        TlsExtension indication = EarlyDataExtension.EncodeIndication();
        TlsExtension maximum = EarlyDataExtension.EncodeMaxEarlyDataSize(16384);
        Diagnostics.Bytes("encoded maximum", maximum.Data);
        Diagnostics.Act("decoded maximum", EarlyDataExtension.DecodeMaxEarlyDataSize(maximum.Data).Value);

        Assert.AreEqual(TlsExtensionType.EarlyData, indication.Type);
        Assert.IsNull(EarlyDataExtension.DecodeIndication(indication.Data));
        Assert.AreEqual(TlsAlertDescription.DecodeError, EarlyDataExtension.DecodeIndication([0]));
        Assert.AreEqual(16384u, EarlyDataExtension.DecodeMaxEarlyDataSize(maximum.Data).Value);
        AssertRejectsTruncationAndTrailingBytes(maximum.Data, EarlyDataExtension.DecodeMaxEarlyDataSize);
    }

    [TestMethod]
    public void ApplicationLayerProtocolNegotiationRoundTrips()
    {
        Diagnostics.Arrange("protocols", "h2, http/1.1");

        TlsExtension extension = ApplicationLayerProtocolNegotiationExtension.Encode(["h2", "http/1.1"]);
        Diagnostics.Act("encoded", Convert.ToHexStringLower(extension.Data));

        Assert.AreEqual(TlsExtensionType.ApplicationLayerProtocolNegotiation, extension.Type);
        Assert.AreEqual("000c02683208687474702f312e31", Convert.ToHexStringLower(extension.Data));
        CollectionAssert.AreEqual(
            new[] { "h2", "http/1.1" },
            ApplicationLayerProtocolNegotiationExtension.Decode(extension.Data).Value.ToArray());
        AssertRejectsTruncationAndTrailingBytes(extension.Data, ApplicationLayerProtocolNegotiationExtension.Decode);
    }

    [TestMethod]
    public void StatusRequestRoundTripsTheOcspRequest()
    {
        Diagnostics.Arrange("requests", "empty; responder ids 0102 and 03 with extensions 0405");
        TlsExtension empty = StatusRequestExtension.EncodeOcspRequest(new OcspStatusRequest([], []));
        TlsExtension full = StatusRequestExtension.EncodeOcspRequest(new OcspStatusRequest([[1, 2], [3]], [4, 5]));

        OcspStatusRequest decoded = StatusRequestExtension.DecodeOcspRequest(full.Data).Value;
        Diagnostics.Bytes("encoded full request", full.Data);
        Diagnostics.Act("decoded responder ids", decoded.ResponderIds.Count);

        Assert.AreEqual(TlsExtensionType.StatusRequest, empty.Type);
        Assert.AreEqual("0100000000", Convert.ToHexStringLower(empty.Data));
        Assert.HasCount(2, decoded.ResponderIds);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, decoded.ResponderIds[0]);
        CollectionAssert.AreEqual(new byte[] { 3 }, decoded.ResponderIds[1]);
        CollectionAssert.AreEqual(new byte[] { 4, 5 }, decoded.RequestExtensions);
        AssertRejectsTruncationAndTrailingBytes(full.Data, StatusRequestExtension.DecodeOcspRequest);
    }

    [TestMethod]
    public void StatusRequestRoundTripsTheStapledResponse()
    {
        Diagnostics.Arrange("ocsp response", "30030a0100");

        TlsExtension extension = StatusRequestExtension.EncodeOcspResponse([0x30, 0x03, 0x0a, 0x01, 0x00]);
        Diagnostics.Act("encoded", Convert.ToHexStringLower(extension.Data));

        Assert.AreEqual("0100000530030a0100", Convert.ToHexStringLower(extension.Data));
        CollectionAssert.AreEqual(new byte[] { 0x30, 0x03, 0x0a, 0x01, 0x00 }, StatusRequestExtension.DecodeOcspResponse(extension.Data).Value);
        AssertRejectsTruncationAndTrailingBytes(extension.Data, StatusRequestExtension.DecodeOcspResponse);
    }

    [TestMethod]
    public void StatusRequestRejectsAStatusTypeOtherThanOcsp()
    {
        Diagnostics.Arrange("data", "request 0200000000 and response 0200000100, status type 2");

        TlsAlertDescription? requestAlert = StatusRequestExtension.DecodeOcspRequest(Convert.FromHexString("0200000000")).Alert;
        TlsAlertDescription? responseAlert = StatusRequestExtension.DecodeOcspResponse(Convert.FromHexString("0200000100")).Alert;
        Diagnostics.Act("alerts", $"{requestAlert}, {responseAlert}");

        Diagnostics.Assert("request alert", TlsAlertDescription.IllegalParameter, requestAlert);
        Diagnostics.Assert("response alert", TlsAlertDescription.IllegalParameter, responseAlert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, requestAlert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, responseAlert);
    }

    [TestMethod]
    public void CookieRoundTrips()
    {
        Diagnostics.Arrange("cookie", "070707");

        TlsExtension extension = CookieExtension.Encode([7, 7, 7]);
        Diagnostics.Bytes("encoded", extension.Data);
        Diagnostics.Act("type", extension.Type);

        Assert.AreEqual(TlsExtensionType.Cookie, extension.Type);
        CollectionAssert.AreEqual(new byte[] { 7, 7, 7 }, CookieExtension.Decode(extension.Data).Value);
        AssertRejectsTruncationAndTrailingBytes(extension.Data, CookieExtension.Decode);
    }

    [TestMethod]
    public void QuicTransportParametersCarriesItsBytesUnchanged()
    {
        byte[] parameters = [0x01, 0x04, 0x80, 0x00, 0x75, 0x30];
        Diagnostics.Arrange("parameters length", parameters.Length);
        Diagnostics.Bytes("parameters", parameters);

        TlsExtension extension = QuicTransportParametersExtension.Encode(parameters);
        Diagnostics.Act("type", extension.Type);

        Diagnostics.Diff("encoded", parameters, extension.Data);
        Assert.AreEqual(TlsExtensionType.QuicTransportParameters, extension.Type);
        CollectionAssert.AreEqual(parameters, extension.Data);
        Assert.AreNotSame(parameters, extension.Data);
        CollectionAssert.AreEqual(parameters, QuicTransportParametersExtension.Decode(extension.Data).Value);
    }

    [TestMethod]
    public void RecordSizeLimitRoundTrips()
    {
        Diagnostics.Arrange("limit", 0x4001);

        TlsExtension extension = RecordSizeLimitExtension.Encode(0x4001);
        Diagnostics.Act("decoded", RecordSizeLimitExtension.Decode(extension.Data).Value);

        Assert.AreEqual(TlsExtensionType.RecordSizeLimit, extension.Type);
        Assert.AreEqual(0x4001, RecordSizeLimitExtension.Decode(extension.Data).Value);
        AssertRejectsTruncationAndTrailingBytes(extension.Data, RecordSizeLimitExtension.Decode);
    }

    [TestMethod]
    public void PaddingRoundTripsAndRejectsANonZeroByte()
    {
        Diagnostics.Arrange("padding length", 5);

        TlsExtension extension = PaddingExtension.Encode(5);
        Diagnostics.Act("decoded", PaddingExtension.Decode(extension.Data).Value);

        Diagnostics.Assert("alert for 0001", TlsAlertDescription.IllegalParameter, PaddingExtension.Decode([0, 1]).Alert);
        Assert.AreEqual(TlsExtensionType.Padding, extension.Type);
        Assert.AreEqual(5, PaddingExtension.Decode(extension.Data).Value);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, PaddingExtension.Decode([0, 1]).Alert);
    }

    [TestMethod]
    public void RenegotiationInfoRoundTrips()
    {
        Diagnostics.Arrange("renegotiated connection", "empty; 010203");

        TlsExtension initial = RenegotiationInfoExtension.Encode([]);
        TlsExtension renegotiated = RenegotiationInfoExtension.Encode([1, 2, 3]);
        Diagnostics.Act("initial data", Convert.ToHexStringLower(initial.Data));

        Assert.AreEqual(TlsExtensionType.RenegotiationInfo, initial.Type);
        Assert.AreEqual("00", Convert.ToHexStringLower(initial.Data));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, RenegotiationInfoExtension.Decode(renegotiated.Data).Value);
        AssertRejectsTruncationAndTrailingBytes(renegotiated.Data, RenegotiationInfoExtension.Decode);
    }

    [TestMethod]
    public void EcPointFormatsRoundTrips()
    {
        Diagnostics.Arrange("formats", "0, 1, 2");

        TlsExtension extension = EcPointFormatsExtension.Encode([0, 1, 2]);
        Diagnostics.Act("encoded", Convert.ToHexStringLower(extension.Data));

        Assert.AreEqual(TlsExtensionType.EcPointFormats, extension.Type);
        Assert.AreEqual("03000102", Convert.ToHexStringLower(extension.Data));
        CollectionAssert.AreEqual(new byte[] { 0, 1, 2 }, EcPointFormatsExtension.Decode(extension.Data).Value);
        AssertRejectsTruncationAndTrailingBytes(extension.Data, EcPointFormatsExtension.Decode);
    }

    [TestMethod]
    public void SessionTicketCarriesTheWholeDataAsTheTicket()
    {
        byte[] ticket = [9, 8, 7];
        Diagnostics.Arrange("forms", "empty request, ticket 090807");
        Diagnostics.Bytes("ticket", ticket);

        TlsExtension request = SessionTicketExtension.Encode([]);
        TlsExtension resumption = SessionTicketExtension.Encode(ticket);
        Diagnostics.Act("data lengths", $"{request.Data.Length}, {resumption.Data.Length}");

        Diagnostics.Diff("resumption data", ticket, resumption.Data);
        Assert.AreEqual(TlsExtensionType.SessionTicket, request.Type);
        Assert.IsEmpty(request.Data);
        Assert.AreNotSame(ticket, resumption.Data);
        CollectionAssert.AreEqual(ticket, SessionTicketExtension.Decode(resumption.Data).Value);
        Assert.IsEmpty(SessionTicketExtension.Decode([]).Value);
    }

    [TestMethod]
    public void EmptyExtensionsAreEmptyAndAnythingElseIsADecodeError()
    {
        (TlsExtension Extension, TlsExtensionType Type, Func<byte[], TlsAlertDescription?> Decode)[] cases =
        [
            (ExtendedMasterSecretExtension.Encode(), TlsExtensionType.ExtendedMasterSecret, ExtendedMasterSecretExtension.Decode),
            (EncryptThenMacExtension.Encode(), TlsExtensionType.EncryptThenMac, EncryptThenMacExtension.Decode),
            (PostHandshakeAuthExtension.Encode(), TlsExtensionType.PostHandshakeAuth, PostHandshakeAuthExtension.Decode),
        ];
        Diagnostics.Arrange("extensions", "extended_master_secret, encrypt_then_mac, post_handshake_auth");

        foreach ((TlsExtension extension, TlsExtensionType type, Func<byte[], TlsAlertDescription?> decode) in cases)
        {
            Diagnostics.Act("extension", $"{extension.Type}, {extension.Data.Length} bytes");
            Diagnostics.Assert("alert for one byte", TlsAlertDescription.DecodeError, decode([0]));
            Assert.AreEqual(type, extension.Type);
            Assert.IsEmpty(extension.Data);
            Assert.IsNull(decode(extension.Data));
            Assert.AreEqual(TlsAlertDescription.DecodeError, decode([0]));
        }
    }

    [TestMethod]
    public void CompressCertificateRoundTrips()
    {
        Diagnostics.Arrange("algorithms", "0x0001, 0x0003");

        TlsExtension extension = CompressCertificateExtension.Encode([0x0001, 0x0003]);
        Diagnostics.Act("encoded", Convert.ToHexStringLower(extension.Data));

        Assert.AreEqual(TlsExtensionType.CompressCertificate, extension.Type);
        Assert.AreEqual("0400010003", Convert.ToHexStringLower(extension.Data));
        CollectionAssert.AreEqual(new ushort[] { 0x0001, 0x0003 }, CompressCertificateExtension.Decode(extension.Data).Value.ToArray());
        AssertRejectsTruncationAndTrailingBytes(extension.Data, CompressCertificateExtension.Decode);
    }

    [TestMethod]
    public void CertificateAuthoritiesRoundTrips()
    {
        TlsExtension extension = CertificateAuthoritiesExtension.Encode([[0x30, 0x00], [0x30, 0x01, 0x05]]);
        Diagnostics.Arrange("names", "3000, 300105");

        IReadOnlyList<byte[]> names = CertificateAuthoritiesExtension.Decode(extension.Data).Value;
        Diagnostics.Act("decoded names", names.Count);

        Assert.AreEqual(TlsExtensionType.CertificateAuthorities, extension.Type);
        Assert.AreEqual("0009000230000003300105", Convert.ToHexStringLower(extension.Data));
        Assert.HasCount(2, names);
        CollectionAssert.AreEqual(new byte[] { 0x30, 0x00 }, names[0]);
        CollectionAssert.AreEqual(new byte[] { 0x30, 0x01, 0x05 }, names[1]);
        AssertRejectsTruncationAndTrailingBytes(extension.Data, CertificateAuthoritiesExtension.Decode);
    }

    [TestMethod]
    public void SrpRoundTripsTheUserName()
    {
        Diagnostics.Arrange("user name", "user");

        TlsExtension extension = SrpExtension.Encode("user"u8.ToArray());
        Diagnostics.Act("encoded", Convert.ToHexStringLower(extension.Data));

        Assert.AreEqual(TlsExtensionType.Srp, extension.Type);
        Assert.AreEqual("0475736572", Convert.ToHexStringLower(extension.Data));
        CollectionAssert.AreEqual("user"u8.ToArray(), SrpExtension.Decode(extension.Data).Value);
        AssertRejectsTruncationAndTrailingBytes(extension.Data, SrpExtension.Decode);
    }

    private void AssertRejectsTruncationAndTrailingBytes<T>(byte[] data, Func<byte[], TlsDecodeResult<T>> decode)
    {
        TlsAlertDescription? truncated = decode(data[..^1]).Alert;
        TlsAlertDescription? trailing = decode([.. data, 0]).Alert;
        Diagnostics.Assert("alert for one byte short", TlsAlertDescription.DecodeError, truncated);
        Diagnostics.Assert("alert for one trailing byte", TlsAlertDescription.DecodeError, trailing);
        Assert.AreEqual(TlsAlertDescription.DecodeError, truncated);
        Assert.AreEqual(TlsAlertDescription.DecodeError, trailing);
    }
}
