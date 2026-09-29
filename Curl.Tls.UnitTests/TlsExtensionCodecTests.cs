namespace Curl.Tls;

/// <summary>
/// Round-trips every extension codec through its encoder and decoder, and checks each
/// decoder answers a truncated length or trailing bytes with <c>decode_error</c> and a
/// value the extension forbids with <c>illegal_parameter</c>.
/// </summary>
[TestClass]
public sealed class TlsExtensionCodecTests
{
    [TestMethod]
    public void ServerNameRoundTripsAHostName()
    {
        TlsExtension extension = ServerNameExtension.EncodeHostName("example.com");

        Assert.AreEqual(TlsExtensionType.ServerName, extension.Type);
        Assert.AreEqual("example.com", ServerNameExtension.DecodeHostName(extension.Data).Value);
        AssertRejectsTruncationAndTrailingBytes(extension.Data, ServerNameExtension.DecodeHostName);
    }

    [TestMethod]
    public void ServerNameRejectsANameTypeOtherThanHostName()
    {
        byte[] data = ServerNameExtension.EncodeHostName("a").Data;
        data[2] = 1;

        Assert.AreEqual(TlsAlertDescription.IllegalParameter, ServerNameExtension.DecodeHostName(data).Alert);
    }

    [TestMethod]
    public void ServerNameRejectsASecondEntryInTheList()
    {
        byte[] data = Convert.FromHexString("000800000161" + "00000162");

        Assert.AreEqual(TlsAlertDescription.DecodeError, ServerNameExtension.DecodeHostName(data).Alert);
    }

    [TestMethod]
    public void ServerNameAcknowledgementIsEmptyAndAnythingElseIsADecodeError()
    {
        TlsExtension extension = ServerNameExtension.EncodeAcknowledgement();

        Assert.AreEqual(TlsExtensionType.ServerName, extension.Type);
        Assert.IsEmpty(extension.Data);
        Assert.IsNull(ServerNameExtension.DecodeAcknowledgement(extension.Data));
        Assert.AreEqual(TlsAlertDescription.DecodeError, ServerNameExtension.DecodeAcknowledgement([0]));
    }

    [TestMethod]
    public void SupportedGroupsRoundTrips()
    {
        TlsExtension extension = SupportedGroupsExtension.Encode([0x11ec, 0x001d, 0x0017]);

        Assert.AreEqual(TlsExtensionType.SupportedGroups, extension.Type);
        CollectionAssert.AreEqual(new ushort[] { 0x11ec, 0x001d, 0x0017 }, SupportedGroupsExtension.Decode(extension.Data).Value.ToArray());
        AssertRejectsTruncationAndTrailingBytes(extension.Data, SupportedGroupsExtension.Decode);
    }

    [TestMethod]
    public void SupportedGroupsRejectsAnOddLengthList()
    {
        Assert.AreEqual(TlsAlertDescription.DecodeError, SupportedGroupsExtension.Decode(Convert.FromHexString("0003001d00")).Alert);
    }

    [TestMethod]
    public void SignatureAlgorithmsRoundTripsInBothExtensions()
    {
        TlsExtension algorithms = SignatureAlgorithmsExtension.Encode([0x0804, 0x0403]);
        TlsExtension certificateAlgorithms = SignatureAlgorithmsExtension.EncodeCertificateSchemes([0x0807]);

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

        IReadOnlyList<KeyShareEntry> shares = KeyShareExtension.DecodeClientShares(extension.Data).Value;

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

        KeyShareEntry share = KeyShareExtension.DecodeServerShare(extension.Data).Value;

        Assert.AreEqual(0x001d, share.Group);
        CollectionAssert.AreEqual(new byte[] { 9, 8 }, share.KeyExchange);
        AssertRejectsTruncationAndTrailingBytes(extension.Data, KeyShareExtension.DecodeServerShare);
    }

    [TestMethod]
    public void KeyShareRoundTripsTheSelectedGroup()
    {
        TlsExtension extension = KeyShareExtension.EncodeSelectedGroup(0x0017);

        Assert.AreEqual(0x0017, KeyShareExtension.DecodeSelectedGroup(extension.Data).Value);
        AssertRejectsTruncationAndTrailingBytes(extension.Data, KeyShareExtension.DecodeSelectedGroup);
    }

    [TestMethod]
    public void SupportedVersionsRoundTripsTheOfferedAndSelectedForms()
    {
        TlsExtension offered = SupportedVersionsExtension.EncodeOffered([0x0304, 0x0303]);
        TlsExtension selected = SupportedVersionsExtension.EncodeSelected(0x0304);

        Assert.AreEqual("0403040303", Convert.ToHexStringLower(offered.Data));
        CollectionAssert.AreEqual(new ushort[] { 0x0304, 0x0303 }, SupportedVersionsExtension.DecodeOffered(offered.Data).Value.ToArray());
        Assert.AreEqual(0x0304, SupportedVersionsExtension.DecodeSelected(selected.Data).Value);
        AssertRejectsTruncationAndTrailingBytes(offered.Data, SupportedVersionsExtension.DecodeOffered);
        AssertRejectsTruncationAndTrailingBytes(selected.Data, SupportedVersionsExtension.DecodeSelected);
    }

    [TestMethod]
    public void PskKeyExchangeModesRoundTrips()
    {
        TlsExtension extension = PskKeyExchangeModesExtension.Encode([1, 0]);

        Assert.AreEqual(TlsExtensionType.PskKeyExchangeModes, extension.Type);
        CollectionAssert.AreEqual(new byte[] { 1, 0 }, PskKeyExchangeModesExtension.Decode(extension.Data).Value);
        AssertRejectsTruncationAndTrailingBytes(extension.Data, PskKeyExchangeModesExtension.Decode);
    }

    [TestMethod]
    public void PreSharedKeyRoundTripsTheOfferAndTheSelection()
    {
        OfferedPsks offer = new([new PskIdentity([1, 2], 0x01020304), new PskIdentity([3], 7)], [[0xaa, 0xbb], [0xcc]]);

        TlsExtension offered = PreSharedKeyExtension.EncodeOffered(offer);
        OfferedPsks decoded = PreSharedKeyExtension.DecodeOffered(offered.Data).Value;
        TlsExtension selected = PreSharedKeyExtension.EncodeSelected(1);

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
        TlsExtension indication = EarlyDataExtension.EncodeIndication();
        TlsExtension maximum = EarlyDataExtension.EncodeMaxEarlyDataSize(16384);

        Assert.AreEqual(TlsExtensionType.EarlyData, indication.Type);
        Assert.IsNull(EarlyDataExtension.DecodeIndication(indication.Data));
        Assert.AreEqual(TlsAlertDescription.DecodeError, EarlyDataExtension.DecodeIndication([0]));
        Assert.AreEqual(16384u, EarlyDataExtension.DecodeMaxEarlyDataSize(maximum.Data).Value);
        AssertRejectsTruncationAndTrailingBytes(maximum.Data, EarlyDataExtension.DecodeMaxEarlyDataSize);
    }

    [TestMethod]
    public void ApplicationLayerProtocolNegotiationRoundTrips()
    {
        TlsExtension extension = ApplicationLayerProtocolNegotiationExtension.Encode(["h2", "http/1.1"]);

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
        TlsExtension empty = StatusRequestExtension.EncodeOcspRequest(new OcspStatusRequest([], []));
        TlsExtension full = StatusRequestExtension.EncodeOcspRequest(new OcspStatusRequest([[1, 2], [3]], [4, 5]));

        OcspStatusRequest decoded = StatusRequestExtension.DecodeOcspRequest(full.Data).Value;

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
        TlsExtension extension = StatusRequestExtension.EncodeOcspResponse([0x30, 0x03, 0x0a, 0x01, 0x00]);

        Assert.AreEqual("0100000530030a0100", Convert.ToHexStringLower(extension.Data));
        CollectionAssert.AreEqual(new byte[] { 0x30, 0x03, 0x0a, 0x01, 0x00 }, StatusRequestExtension.DecodeOcspResponse(extension.Data).Value);
        AssertRejectsTruncationAndTrailingBytes(extension.Data, StatusRequestExtension.DecodeOcspResponse);
    }

    [TestMethod]
    public void StatusRequestRejectsAStatusTypeOtherThanOcsp()
    {
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, StatusRequestExtension.DecodeOcspRequest(Convert.FromHexString("0200000000")).Alert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, StatusRequestExtension.DecodeOcspResponse(Convert.FromHexString("0200000100")).Alert);
    }

    [TestMethod]
    public void CookieRoundTrips()
    {
        TlsExtension extension = CookieExtension.Encode([7, 7, 7]);

        Assert.AreEqual(TlsExtensionType.Cookie, extension.Type);
        CollectionAssert.AreEqual(new byte[] { 7, 7, 7 }, CookieExtension.Decode(extension.Data).Value);
        AssertRejectsTruncationAndTrailingBytes(extension.Data, CookieExtension.Decode);
    }

    [TestMethod]
    public void QuicTransportParametersCarriesItsBytesUnchanged()
    {
        byte[] parameters = [0x01, 0x04, 0x80, 0x00, 0x75, 0x30];

        TlsExtension extension = QuicTransportParametersExtension.Encode(parameters);

        Assert.AreEqual(TlsExtensionType.QuicTransportParameters, extension.Type);
        CollectionAssert.AreEqual(parameters, extension.Data);
        Assert.AreNotSame(parameters, extension.Data);
        CollectionAssert.AreEqual(parameters, QuicTransportParametersExtension.Decode(extension.Data).Value);
    }

    [TestMethod]
    public void RecordSizeLimitRoundTrips()
    {
        TlsExtension extension = RecordSizeLimitExtension.Encode(0x4001);

        Assert.AreEqual(TlsExtensionType.RecordSizeLimit, extension.Type);
        Assert.AreEqual(0x4001, RecordSizeLimitExtension.Decode(extension.Data).Value);
        AssertRejectsTruncationAndTrailingBytes(extension.Data, RecordSizeLimitExtension.Decode);
    }

    [TestMethod]
    public void PaddingRoundTripsAndRejectsANonZeroByte()
    {
        TlsExtension extension = PaddingExtension.Encode(5);

        Assert.AreEqual(TlsExtensionType.Padding, extension.Type);
        Assert.AreEqual(5, PaddingExtension.Decode(extension.Data).Value);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, PaddingExtension.Decode([0, 1]).Alert);
    }

    [TestMethod]
    public void RenegotiationInfoRoundTrips()
    {
        TlsExtension initial = RenegotiationInfoExtension.Encode([]);
        TlsExtension renegotiated = RenegotiationInfoExtension.Encode([1, 2, 3]);

        Assert.AreEqual(TlsExtensionType.RenegotiationInfo, initial.Type);
        Assert.AreEqual("00", Convert.ToHexStringLower(initial.Data));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, RenegotiationInfoExtension.Decode(renegotiated.Data).Value);
        AssertRejectsTruncationAndTrailingBytes(renegotiated.Data, RenegotiationInfoExtension.Decode);
    }

    [TestMethod]
    public void EcPointFormatsRoundTrips()
    {
        TlsExtension extension = EcPointFormatsExtension.Encode([0, 1, 2]);

        Assert.AreEqual(TlsExtensionType.EcPointFormats, extension.Type);
        Assert.AreEqual("03000102", Convert.ToHexStringLower(extension.Data));
        CollectionAssert.AreEqual(new byte[] { 0, 1, 2 }, EcPointFormatsExtension.Decode(extension.Data).Value);
        AssertRejectsTruncationAndTrailingBytes(extension.Data, EcPointFormatsExtension.Decode);
    }

    [TestMethod]
    public void SessionTicketCarriesTheWholeDataAsTheTicket()
    {
        byte[] ticket = [9, 8, 7];

        TlsExtension request = SessionTicketExtension.Encode([]);
        TlsExtension resumption = SessionTicketExtension.Encode(ticket);

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

        foreach ((TlsExtension extension, TlsExtensionType type, Func<byte[], TlsAlertDescription?> decode) in cases)
        {
            Assert.AreEqual(type, extension.Type);
            Assert.IsEmpty(extension.Data);
            Assert.IsNull(decode(extension.Data));
            Assert.AreEqual(TlsAlertDescription.DecodeError, decode([0]));
        }
    }

    [TestMethod]
    public void CompressCertificateRoundTrips()
    {
        TlsExtension extension = CompressCertificateExtension.Encode([0x0001, 0x0003]);

        Assert.AreEqual(TlsExtensionType.CompressCertificate, extension.Type);
        Assert.AreEqual("0400010003", Convert.ToHexStringLower(extension.Data));
        CollectionAssert.AreEqual(new ushort[] { 0x0001, 0x0003 }, CompressCertificateExtension.Decode(extension.Data).Value.ToArray());
        AssertRejectsTruncationAndTrailingBytes(extension.Data, CompressCertificateExtension.Decode);
    }

    [TestMethod]
    public void CertificateAuthoritiesRoundTrips()
    {
        TlsExtension extension = CertificateAuthoritiesExtension.Encode([[0x30, 0x00], [0x30, 0x01, 0x05]]);

        IReadOnlyList<byte[]> names = CertificateAuthoritiesExtension.Decode(extension.Data).Value;

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
        TlsExtension extension = SrpExtension.Encode("user"u8.ToArray());

        Assert.AreEqual(TlsExtensionType.Srp, extension.Type);
        Assert.AreEqual("0475736572", Convert.ToHexStringLower(extension.Data));
        CollectionAssert.AreEqual("user"u8.ToArray(), SrpExtension.Decode(extension.Data).Value);
        AssertRejectsTruncationAndTrailingBytes(extension.Data, SrpExtension.Decode);
    }

    private static void AssertRejectsTruncationAndTrailingBytes<T>(byte[] data, Func<byte[], TlsDecodeResult<T>> decode)
    {
        Assert.AreEqual(TlsAlertDescription.DecodeError, decode(data[..^1]).Alert);
        Assert.AreEqual(TlsAlertDescription.DecodeError, decode([.. data, 0]).Alert);
    }
}
