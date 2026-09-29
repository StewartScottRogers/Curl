using System.Formats.Asn1;

namespace Curl.Kerberos;

/// <summary>
/// Round-trips every message and structure built from RFC 4120's definitions with every
/// optional field present, which the recorded exchange leaves out, and pins the AP-REP and
/// <c>EncAPRepPart</c>, which it has none of, to DER written out by hand from RFC 4120's ASN.1.
/// </summary>
[TestClass]
public sealed class KerberosMessageRoundTripTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 3, 30, 43, TimeSpan.Zero);

    private static readonly KerberosPrincipalName Alice = new(1, ["alice"]);

    private static readonly KerberosPrincipalName Service = new(2, ["HTTP", "server.example.test"]);

    private static readonly KerberosEncryptedData Cipher = new(18, 2, [0x01, 0x02, 0x03, 0x04]);

    private static readonly KerberosTicket Ticket = new("EXAMPLE.TEST", Service, Cipher);

    // AP-REP ::= [APPLICATION 15] SEQUENCE { pvno [0] 5, msg-type [1] 15, enc-part [2] EncryptedData { etype [0] 18, cipher [2] 01020304 } }
    private const string ApReplyDer = "6f1d 301b a003020105 a10302010f a20f 300d a003020112 a206 040401020304";

    // EncAPRepPart ::= [APPLICATION 27] SEQUENCE { ctime [0] 20260929033043Z, cusec [1] 310692, seq-number [3] 0x12345678 }
    private const string EncryptedApReplyPartDer = "7b24 3022 a011180f32303236303932393033333034335a a105020304bda4 a306020412345678";

    [TestMethod]
    public void Encode_ApReply_GivesTheDerWrittenFromRfc4120()
    {
        KerberosApReply reply = new(new KerberosEncryptedData(18, null, [0x01, 0x02, 0x03, 0x04]));

        CollectionAssert.AreEqual(Hex.Bytes(ApReplyDer), reply.Encode());
    }

    [TestMethod]
    public void Decode_ApReplyDer_GivesItsEncryptedPart()
    {
        KerberosApReply reply = KerberosApReply.Decode(Hex.Bytes(ApReplyDer));

        Assert.AreEqual(18, reply.EncryptedPart.EncryptionType);
        Assert.IsNull(reply.EncryptedPart.KeyVersionNumber);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x02, 0x03, 0x04 }, reply.EncryptedPart.Cipher);
        CollectionAssert.AreEqual(Hex.Bytes(ApReplyDer), reply.Encode());
    }

    [TestMethod]
    public void Encode_EncryptedApReplyPart_GivesTheDerWrittenFromRfc4120()
    {
        using KerberosEncryptedApReplyPart part = new() { ClientTime = Now, ClientMicroseconds = 310692, SequenceNumber = 0x12345678 };

        CollectionAssert.AreEqual(Hex.Bytes(EncryptedApReplyPartDer), part.Encode());
    }

    [TestMethod]
    public void Decode_EncryptedApReplyPartDer_GivesItsFields()
    {
        using KerberosEncryptedApReplyPart part = KerberosEncryptedApReplyPart.Decode(Hex.Bytes(EncryptedApReplyPartDer));

        Assert.AreEqual(Now, part.ClientTime);
        Assert.AreEqual(310692, part.ClientMicroseconds);
        Assert.IsNull(part.Subkey);
        Assert.AreEqual(0x12345678u, part.SequenceNumber);
    }

    [TestMethod]
    public void RoundTrip_EncryptedApReplyPartWithSubkey_KeepsEveryField()
    {
        using KerberosEncryptedApReplyPart part = new() { ClientTime = Now, ClientMicroseconds = 0, Subkey = new KerberosKey(17, Key(16)), SequenceNumber = 0xFEDCBA98 };
        byte[] encoded = part.Encode();

        using KerberosEncryptedApReplyPart decoded = KerberosEncryptedApReplyPart.Decode(encoded);

        Assert.AreEqual(17, decoded.Subkey!.EncryptionType);
        CollectionAssert.AreEqual(Key(16), decoded.Subkey.Value.ToArray());
        Assert.AreEqual(0xFEDCBA98u, decoded.SequenceNumber);
        CollectionAssert.AreEqual(encoded, decoded.Encode());
    }

    [TestMethod]
    public void Decode_NegativeSequenceNumber_ReadsItsTwosComplement()
    {
        // EncAPRepPart { ctime [0], cusec [1] 0, seq-number [3] -1 }, as signed writers send 0xFFFFFFFF.
        byte[] part = Hex.Bytes("7b1f 301d a011180f32303236303932393033333034335a a103020100 a3030201ff");

        using KerberosEncryptedApReplyPart decoded = KerberosEncryptedApReplyPart.Decode(part);

        Assert.AreEqual(uint.MaxValue, decoded.SequenceNumber);
    }

    [TestMethod]
    public void RoundTrip_KdcRequestWithEveryField_KeepsEveryField()
    {
        KerberosKdcRequest request = new()
        {
            MessageType = KerberosMessageType.TgsRequest,
            PreAuthenticationData = [new KerberosPreAuthenticationData(KerberosPreAuthenticationData.TgsRequest, [0x6e, 0x00])],
            Body = new KerberosKdcRequestBody
            {
                Options = KerberosKdcOptions.Renewable | KerberosKdcOptions.EncryptTicketInSessionKey | KerberosKdcOptions.Validate,
                ClientName = Alice,
                Realm = "EXAMPLE.TEST",
                ServerName = Service,
                From = Now,
                Till = Now.AddHours(10),
                RenewTill = Now.AddDays(7),
                Nonce = 0xFFFFFFFF,
                EncryptionTypes = [18, 17],
                Addresses = [new KerberosAddress(2, [127, 0, 0, 1])],
                EncryptedAuthorizationData = Cipher,
                AdditionalTickets = [Ticket],
            },
        };
        byte[] encoded = request.Encode();

        KerberosKdcRequest decoded = KerberosKdcRequest.Decode(encoded);

        Assert.AreEqual(KerberosMessageType.TgsRequest, decoded.MessageType);
        Assert.AreEqual(request.Body.Options, decoded.Body.Options);
        Assert.AreEqual(Now, decoded.Body.From);
        Assert.AreEqual(Now.AddDays(7), decoded.Body.RenewTill);
        Assert.AreEqual(0xFFFFFFFFu, decoded.Body.Nonce);
        Assert.AreEqual(2, decoded.Body.Addresses.Single().AddressType);
        CollectionAssert.AreEqual(new byte[] { 127, 0, 0, 1 }, decoded.Body.Addresses.Single().Address);
        Assert.AreEqual(2u, decoded.Body.EncryptedAuthorizationData!.KeyVersionNumber);
        Assert.AreEqual("EXAMPLE.TEST", decoded.Body.AdditionalTickets.Single().Realm);
        CollectionAssert.AreEqual(encoded, decoded.Encode());
    }

    [TestMethod]
    public void RoundTrip_KdcReply_KeepsEveryField()
    {
        KerberosKdcReply reply = new()
        {
            MessageType = KerberosMessageType.AsReply,
            ClientRealm = "EXAMPLE.TEST",
            ClientName = Alice,
            Ticket = Ticket,
            EncryptedPart = Cipher,
        };
        byte[] encoded = reply.Encode();

        KerberosKdcReply decoded = KerberosKdcReply.Decode(encoded);

        Assert.AreEqual(KerberosMessageType.AsReply, decoded.MessageType);
        Assert.IsEmpty(decoded.PreAuthenticationData);
        CollectionAssert.AreEqual(encoded, decoded.Encode());
    }

    [TestMethod]
    public void RoundTrip_EncryptedAsReplyPartWithEveryField_KeepsTag25AndEveryField()
    {
        using KerberosEncryptedKdcReplyPart part = new()
        {
            ReplyType = KerberosMessageType.AsReply,
            Key = new KerberosKey(18, Key(32)),
            LastRequests = [new KerberosLastRequest(6, Now.AddDays(90))],
            Nonce = 42,
            KeyExpiration = Now.AddDays(90),
            Flags = KerberosTicketFlags.Renewable | KerberosTicketFlags.Anonymous,
            AuthenticationTime = Now,
            StartTime = Now.AddMinutes(1),
            EndTime = Now.AddHours(10),
            RenewUntil = Now.AddDays(7),
            ServerRealm = "EXAMPLE.TEST",
            ServerName = Service,
            ClientAddresses = [new KerberosAddress(24, new byte[16])],
            EncryptedPreAuthenticationData = [new KerberosPreAuthenticationData(149, [])],
        };
        byte[] encoded = part.Encode();

        using KerberosEncryptedKdcReplyPart decoded = KerberosEncryptedKdcReplyPart.Decode(encoded);

        Assert.AreEqual(0x79, encoded[0], "[APPLICATION 25], constructed.");
        Assert.AreEqual(KerberosMessageType.AsReply, decoded.ReplyType);
        Assert.AreEqual(Now.AddDays(90), decoded.KeyExpiration);
        Assert.AreEqual(Now.AddMinutes(1), decoded.StartTime);
        Assert.AreEqual(Now.AddDays(7), decoded.RenewUntil);
        Assert.AreEqual(part.Flags, decoded.Flags);
        Assert.AreEqual(24, decoded.ClientAddresses.Single().AddressType);
        CollectionAssert.AreEqual(encoded, decoded.Encode());
    }

    [TestMethod]
    public void Dispose_EncryptedKdcReplyPart_ZeroesTheSessionKey()
    {
        KerberosEncryptedKdcReplyPart part = new()
        {
            ReplyType = KerberosMessageType.TgsReply,
            Key = new KerberosKey(18, Key(32)),
            LastRequests = [],
            Nonce = 1,
            Flags = KerberosTicketFlags.None,
            AuthenticationTime = Now,
            EndTime = Now,
            ServerRealm = "EXAMPLE.TEST",
            ServerName = Service,
        };

        part.Dispose();

        Assert.IsFalse(part.Key.Value.ContainsAnyExcept((byte)0));
    }

    [TestMethod]
    public void RoundTrip_AuthenticatorWithEveryField_KeepsEveryField()
    {
        KerberosAuthenticator authenticator = new()
        {
            ClientRealm = "EXAMPLE.TEST",
            ClientName = Alice,
            Checksum = new KerberosChecksum(0x8003, new byte[24]),
            ClientMicroseconds = 999999,
            ClientTime = Now,
            Subkey = new KerberosKey(18, Key(32)),
            SequenceNumber = 7,
            AuthorizationData = [new KerberosAuthorizationData(1, [0x30, 0x00])],
        };
        byte[] encoded = authenticator.Encode();

        using KerberosAuthenticator decoded = KerberosAuthenticator.Decode(encoded);
        authenticator.Dispose();

        Assert.AreEqual(0x8003, decoded.Checksum!.ChecksumType);
        Assert.AreEqual(7u, decoded.SequenceNumber);
        Assert.AreEqual(1, decoded.AuthorizationData.Single().DataType);
        CollectionAssert.AreEqual(encoded, decoded.Encode());
        Assert.IsFalse(authenticator.Subkey!.Value.ContainsAnyExcept((byte)0));
    }

    [TestMethod]
    public void Dispose_WithoutSubkey_DoesNothing()
    {
        KerberosAuthenticator authenticator = new() { ClientRealm = "EXAMPLE.TEST", ClientName = Alice, ClientMicroseconds = 0, ClientTime = Now };
        KerberosEncryptedApReplyPart part = new() { ClientTime = Now, ClientMicroseconds = 0 };

        authenticator.Dispose();
        part.Dispose();

        Assert.IsNull(authenticator.Subkey);
        Assert.IsNull(part.Subkey);
    }

    [TestMethod]
    public void RoundTrip_ApRequest_KeepsEveryField()
    {
        KerberosApRequest request = new(KerberosApOptions.UseSessionKey | KerberosApOptions.MutualRequired, Ticket, Cipher);
        byte[] encoded = request.Encode();

        KerberosApRequest decoded = KerberosApRequest.Decode(encoded);

        Assert.AreEqual(request.Options, decoded.Options);
        CollectionAssert.AreEqual(encoded, decoded.Encode());
    }

    [TestMethod]
    public void RoundTrip_ErrorWithEveryField_KeepsEveryField()
    {
        KerberosErrorMessage error = new()
        {
            ClientTime = Now,
            ClientMicroseconds = 5,
            ServerTime = Now,
            ServerMicroseconds = 6,
            ErrorCode = 37,
            ClientRealm = "EXAMPLE.TEST",
            ClientName = Alice,
            Realm = "EXAMPLE.TEST",
            ServerName = Service,
            ErrorText = "Clock skew too great",
            ErrorData = [0x30, 0x00],
        };
        byte[] encoded = error.Encode();

        KerberosErrorMessage decoded = KerberosErrorMessage.Decode(encoded);

        Assert.AreEqual(Now, decoded.ClientTime);
        Assert.AreEqual(5, decoded.ClientMicroseconds);
        Assert.AreEqual(37, decoded.ErrorCode);
        CollectionAssert.AreEqual(encoded, decoded.Encode());
    }

    [TestMethod]
    public void RoundTrip_ErrorWithoutOptionalFields_LeavesThemOut()
    {
        KerberosErrorMessage error = new() { ServerTime = Now, ServerMicroseconds = 0, ErrorCode = 6, Realm = "EXAMPLE.TEST", ServerName = Service };
        byte[] encoded = error.Encode();

        KerberosErrorMessage decoded = KerberosErrorMessage.Decode(encoded);

        Assert.IsNull(decoded.ClientRealm);
        Assert.IsNull(decoded.ClientName);
        Assert.IsNull(decoded.ErrorText);
        Assert.IsNull(decoded.ErrorData);
        CollectionAssert.AreEqual(encoded, decoded.Encode());
    }

    [TestMethod]
    public void RoundTrip_EncryptionTypeInfo2WithEveryField_KeepsEveryField()
    {
        byte[] encoded = KerberosEncryptionTypeInfo2Entry.EncodeList([new KerberosEncryptionTypeInfo2Entry(18, "EXAMPLE.TESTalice", [0x00, 0x00, 0x10, 0x00]), new KerberosEncryptionTypeInfo2Entry(23, null, null)]);

        IReadOnlyList<KerberosEncryptionTypeInfo2Entry> decoded = KerberosEncryptionTypeInfo2Entry.DecodeList(encoded);

        CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x10, 0x00 }, decoded[0].StringToKeyParameters);
        Assert.IsNull(decoded[1].Salt);
        CollectionAssert.AreEqual(encoded, KerberosEncryptionTypeInfo2Entry.EncodeList(decoded));
    }

    [TestMethod]
    public void RoundTrip_EncryptedTimestampWithoutMicroseconds_LeavesThemOut()
    {
        KerberosEncryptedTimestamp timestamp = new(Now.AddTicks(1234567), null);

        KerberosEncryptedTimestamp decoded = KerberosEncryptedTimestamp.Decode(timestamp.Encode());

        Assert.AreEqual(Now, decoded.Timestamp, "KerberosTime carries no fraction of a second.");
        Assert.IsNull(decoded.Microseconds);
    }

    [TestMethod]
    public void Encode_NonAsciiRealm_WritesUtf8InAGeneralString()
    {
        KerberosTicket ticket = new("ÉXAMPLE", Service, Cipher);

        byte[] encoded = ticket.Encode();

        Assert.AreEqual("ÉXAMPLE", KerberosTicket.Decode(encoded).Realm);
        Assert.IsTrue(encoded.AsSpan().IndexOf(Hex.Bytes("a10a 1b08 c38958414d504c45")) > 0);
    }

    [TestMethod]
    [DataRow("03020060", 0x60000000u, DisplayName = "Shorter than 32 bits")]
    [DataRow("03060040000000ff", 0x40000000u, DisplayName = "Longer than 32 bits")]
    [DataRow("030100", 0u, DisplayName = "Empty")]
    public void ReadFlags_BitStringOtherThan32Bits_ReadsTheFirst32PaddedWithZeros(string hex, uint expected)
    {
        AsnReader reader = new(Hex.Bytes(hex), AsnEncodingRules.BER);

        Assert.AreEqual(expected, KerberosAsn1.ReadFlags(reader));
    }

    [TestMethod]
    public void RoundTrip_KeyVersionNumberWithTheHighBitSet_KeepsItUnsigned()
    {
        // A Windows read-only DC puts its number in the key version number's upper 16 bits.
        KerberosEncryptedData data = new(18, 0x80010002, [0x00]);
        byte[] encoded = data.Encode();

        KerberosEncryptedData decoded = KerberosEncryptedData.Decode(encoded);

        Assert.AreEqual(0x80010002u, decoded.KeyVersionNumber);
        CollectionAssert.AreEqual(Hex.Bytes("3013 a003020112 a1070205 0080010002 a203040100"), encoded);
    }

    [TestMethod]
    public void Encode_KdcRequestOfAnotherType_ThrowsInvalidOperation()
    {
        KerberosKdcRequest request = new()
        {
            MessageType = KerberosMessageType.ApRequest,
            Body = new KerberosKdcRequestBody { Options = KerberosKdcOptions.None, Realm = "EXAMPLE.TEST", Till = Now, Nonce = 1, EncryptionTypes = [18] },
        };

        Assert.ThrowsExactly<InvalidOperationException>(() => request.Encode());
    }

    [TestMethod]
    public void Encode_KdcReplyOfAnotherType_ThrowsInvalidOperation()
    {
        KerberosKdcReply reply = new() { MessageType = KerberosMessageType.Error, ClientRealm = "EXAMPLE.TEST", ClientName = Alice, Ticket = Ticket, EncryptedPart = Cipher };

        Assert.ThrowsExactly<InvalidOperationException>(() => reply.Encode());
    }

    [TestMethod]
    public void Encode_EncryptedKdcReplyPartOfAnotherType_ThrowsInvalidOperation()
    {
        using KerberosEncryptedKdcReplyPart part = new()
        {
            ReplyType = KerberosMessageType.ApReply,
            Key = new KerberosKey(18, Key(32)),
            LastRequests = [],
            Nonce = 1,
            Flags = KerberosTicketFlags.None,
            AuthenticationTime = Now,
            EndTime = Now,
            ServerRealm = "EXAMPLE.TEST",
            ServerName = Service,
        };

        Assert.ThrowsExactly<InvalidOperationException>(() => part.Encode());
    }

    [TestMethod]
    public void DisposeKeyOnFailure_RestFails_ZeroesTheKeyAndRethrows()
    {
        KerberosKey key = new(18, Key(32));

        Assert.ThrowsExactly<AsnContentException>(() => KerberosAsn1.DisposeKeyOnFailure<int>(key, () => throw new AsnContentException()));

        Assert.IsFalse(key.Value.ContainsAnyExcept((byte)0));
    }

    [TestMethod]
    public void DisposeKeyOnFailure_NoKeyAndRestFails_Rethrows()
    {
        Assert.ThrowsExactly<AsnContentException>(() => KerberosAsn1.DisposeKeyOnFailure<int>(null, () => throw new AsnContentException()));
    }

    [TestMethod]
    public void DecodeValue_BytesLeftAfterAKey_ZeroesTheKeyAndFailsAsMalformed()
    {
        // EncryptionKey { keytype [0] 18, keyvalue [1] 0102 }, then a stray byte.
        byte[] bytes = Hex.Bytes("300b a003020112 a1040402 0102 00");
        KerberosKey? read = null;

        KerberosMessageException failure = Assert.ThrowsExactly<KerberosMessageException>(() => KerberosAsn1.DecodeValue(bytes, reader => read = KerberosAsn1.KeyReader(reader)));

        Assert.AreEqual(KerberosMessageError.Malformed, failure.Error);
        Assert.IsFalse(read!.Value.ContainsAnyExcept((byte)0));
    }

    [TestMethod]
    public void Decode_PartsFailingAfterTheirKey_FailAsMalformed()
    {
        using KerberosEncryptedApReplyPart apReplyPart = new() { ClientTime = Now, ClientMicroseconds = 0, Subkey = new KerberosKey(17, Key(16)), SequenceNumber = 1 };
        using KerberosAuthenticator authenticator = new() { ClientRealm = "EXAMPLE.TEST", ClientName = Alice, ClientMicroseconds = 0, ClientTime = Now, Subkey = new KerberosKey(17, Key(16)), SequenceNumber = 1 };
        byte[] apReplyPartBytes = apReplyPart.Encode();
        byte[] authenticatorBytes = authenticator.Encode();
        byte[] kdcReplyPartBytes = KerberosEncryptedKdcReplyPart.Decode(KdcReplyPartBytes()).Encode();

        // In each, an integer field after the key is turned into an OCTET STRING of the same length.
        AssertMalformed(() => KerberosEncryptedApReplyPart.Decode(IntegerMadeOctets(apReplyPartBytes, "a303020101")));
        AssertMalformed(() => KerberosAuthenticator.Decode(IntegerMadeOctets(authenticatorBytes, "a703020101")));
        AssertMalformed(() => KerberosEncryptedKdcReplyPart.Decode(IntegerMadeOctets(kdcReplyPartBytes, "a203020101")));
    }

    private static byte[] KdcReplyPartBytes()
    {
        using KerberosEncryptedKdcReplyPart part = new()
        {
            ReplyType = KerberosMessageType.TgsReply,
            Key = new KerberosKey(18, Key(32)),
            LastRequests = [],
            Nonce = 1,
            Flags = KerberosTicketFlags.None,
            AuthenticationTime = Now,
            EndTime = Now,
            ServerRealm = "EXAMPLE.TEST",
            ServerName = Service,
        };
        return part.Encode();
    }

    private static byte[] IntegerMadeOctets(byte[] encoded, string fieldHex)
    {
        byte[] damaged = [.. encoded];
        int field = damaged.AsSpan().IndexOf(Hex.Bytes(fieldHex));
        Assert.IsGreaterThan(0, field);
        damaged[field + 2] = 0x04;
        return damaged;
    }

    private static void AssertMalformed(Action decode)
    {
        KerberosMessageException failure = Assert.ThrowsExactly<KerberosMessageException>(decode);

        Assert.AreEqual(KerberosMessageError.Malformed, failure.Error);
    }

    private static byte[] Key(int length) => [.. Enumerable.Range(1, length).Select(value => (byte)value)];
}
