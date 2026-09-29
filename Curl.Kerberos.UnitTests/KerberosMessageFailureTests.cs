namespace Curl.Kerberos;

/// <summary>
/// Checks that bytes which are not the message asked for fail with a typed
/// <see cref="KerberosMessageException" />, never an ASN.1 exception: another message's
/// application tag, a truncated or overlong message, a wrong version, a <c>msg-type</c> that
/// disagrees with the tag, and values out of range.
/// </summary>
[TestClass]
public sealed class KerberosMessageFailureTests
{
    [TestMethod]
    public void Decode_AnotherMessagesApplicationTag_FailsAsUnexpectedMessage()
    {
        byte[] asReply = RecordedKerberosMessages.AsReply;

        AssertFails(KerberosMessageError.UnexpectedMessage, () => KerberosErrorMessage.Decode(asReply));
        AssertFails(KerberosMessageError.UnexpectedMessage, () => KerberosKdcRequest.Decode(asReply));
        AssertFails(KerberosMessageError.UnexpectedMessage, () => KerberosKdcReply.Decode(RecordedKerberosMessages.PreAuthenticationRequiredError));
        AssertFails(KerberosMessageError.UnexpectedMessage, () => KerberosApRequest.Decode(asReply));
        AssertFails(KerberosMessageError.UnexpectedMessage, () => KerberosApReply.Decode(asReply));
        AssertFails(KerberosMessageError.UnexpectedMessage, () => KerberosTicket.Decode(asReply));
        AssertFails(KerberosMessageError.UnexpectedMessage, () => KerberosAuthenticator.Decode(asReply));
        AssertFails(KerberosMessageError.UnexpectedMessage, () => KerberosEncryptedApReplyPart.Decode(asReply));
        AssertFails(KerberosMessageError.UnexpectedMessage, () => KerberosEncryptedKdcReplyPart.Decode(asReply));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(4)]
    [DataRow(13)]
    [DataRow(100)]
    [DataRow(801)]
    public void Decode_TruncatedAsReply_FailsAsMalformed(int length)
    {
        byte[] truncated = RecordedKerberosMessages.AsReply[..length];

        AssertFails(KerberosMessageError.Malformed, () => KerberosKdcReply.Decode(truncated));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(2)]
    [DataRow(200)]
    public void Decode_TruncatedError_FailsAsMalformed(int length)
    {
        byte[] truncated = RecordedKerberosMessages.PreAuthenticationRequiredError[..length];

        AssertFails(KerberosMessageError.Malformed, () => KerberosErrorMessage.Decode(truncated));
    }

    [TestMethod]
    public void Decode_ByteAfterTheMessage_FailsAsMalformed()
    {
        byte[] overlong = [.. RecordedKerberosMessages.AsReply, 0x00];

        AssertFails(KerberosMessageError.Malformed, () => KerberosKdcReply.Decode(overlong));
    }

    [TestMethod]
    public void Decode_ProtocolVersionFour_FailsAsUnsupportedVersion()
    {
        byte[] versionFour = [.. RecordedKerberosMessages.AsReply];
        versionFour[12] = 4;

        AssertFails(KerberosMessageError.UnsupportedVersion, () => KerberosKdcReply.Decode(versionFour));
    }

    [TestMethod]
    public void Decode_MessageTypeDisagreeingWithTheTag_FailsAsUnexpectedMessage()
    {
        byte[] tgsReplyType = [.. RecordedKerberosMessages.AsReply];
        tgsReplyType[17] = (byte)KerberosMessageType.TgsReply;

        AssertFails(KerberosMessageError.UnexpectedMessage, () => KerberosKdcReply.Decode(tgsReplyType));
    }

    [TestMethod]
    public void Decode_TicketVersionFour_FailsAsUnsupportedVersion()
    {
        // Ticket ::= [APPLICATION 1] SEQUENCE { tkt-vno [0] 4, ... }; the version is checked before the rest.
        byte[] ticket = Hex.Bytes("6107 3005 a003020104");

        AssertFails(KerberosMessageError.UnsupportedVersion, () => KerberosTicket.Decode(ticket));
    }

    [TestMethod]
    public void Decode_AuthenticatorVersionFour_FailsAsUnsupportedVersion()
    {
        byte[] authenticator = Hex.Bytes("6207 3005 a003020104");

        AssertFails(KerberosMessageError.UnsupportedVersion, () => KerberosAuthenticator.Decode(authenticator));
    }

    [TestMethod]
    public void Decode_EncryptionTypeBeyond32Bits_FailsAsMalformed()
    {
        // EncryptedData { etype [0] 2^31, cipher [2] 00 }.
        byte[] encryptedData = Hex.Bytes("300e a007 02050080000000 a203 040100");

        AssertFails(KerberosMessageError.Malformed, () => KerberosEncryptedData.Decode(encryptedData));
    }

    [TestMethod]
    public void Decode_SequenceNumberBeyond32Bits_FailsAsMalformed()
    {
        // EncAPRepPart { ctime [0], cusec [1] 0, seq-number [3] 2^32 }.
        byte[] part = Hex.Bytes("7b23 3021 a011180f32303236303932393033333034335a a103020100 a307020501 00000000");

        AssertFails(KerberosMessageError.Malformed, () => KerberosEncryptedApReplyPart.Decode(part));
    }

    [TestMethod]
    public void Decode_ConstructedGeneralString_FailsAsMalformed()
    {
        // ETYPE-INFO2 { { etype [0] 18, salt [1] a constructed (BER) GeneralString "abc" } }.
        byte[] list = Hex.Bytes("3010 300e a003020112 a107 3b05 0403616263");

        AssertFails(KerberosMessageError.Malformed, () => KerberosEncryptionTypeInfo2Entry.DecodeList(list));
    }

    [TestMethod]
    public void Decode_MissingRequiredField_FailsAsMalformed()
    {
        // PA-ENC-TS-ENC with pausec [1] but no patimestamp [0].
        byte[] timestamp = Hex.Bytes("3007 a105 020300fdb1");

        AssertFails(KerberosMessageError.Malformed, () => KerberosEncryptedTimestamp.Decode(timestamp));
    }

    [TestMethod]
    public void Decode_MethodDataThatIsNotASequence_FailsAsMalformed()
    {
        AssertFails(KerberosMessageError.Malformed, () => KerberosPreAuthenticationData.DecodeMethodData(Hex.Bytes("0400")));
    }

    [TestMethod]
    [DataRow("3000", DisplayName = "Universal SEQUENCE")]
    [DataRow("aa00", DisplayName = "Context [10], a message type's number in another class")]
    [DataRow("6100", DisplayName = "[APPLICATION 1], a Ticket")]
    [DataRow("7f1f00", DisplayName = "[APPLICATION 31]")]
    [DataRow("4a00", DisplayName = "Primitive [APPLICATION 10]")]
    public void PeekType_TagThatIsNoMessage_FailsAsUnexpectedMessage(string hex)
    {
        AssertFails(KerberosMessageError.UnexpectedMessage, () => KerberosMessage.PeekType(Hex.Bytes(hex)));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("6a05")]
    [DataRow("6a00 00")]
    public void PeekType_NotOneWholeValue_FailsAsMalformed(string hex)
    {
        AssertFails(KerberosMessageError.Malformed, () => KerberosMessage.PeekType(Hex.Bytes(hex)));
    }

    [TestMethod]
    public void Exception_SaysWhy()
    {
        KerberosMessageException exception = new(KerberosMessageError.UnsupportedVersion);

        Assert.AreEqual(KerberosMessageError.UnsupportedVersion, exception.Error);
        Assert.AreEqual("Kerberos message could not be decoded: UnsupportedVersion.", exception.Message);
    }

    private static void AssertFails(KerberosMessageError expected, Action decode)
    {
        KerberosMessageException failure = Assert.ThrowsExactly<KerberosMessageException>(decode);

        Assert.AreEqual(expected, failure.Error);
    }
}
