using System.Text;

namespace Curl.Kerberos;

/// <summary>
/// Pins the message decoders and encoders to the exchange MIT's <c>kinit</c> and <c>kvno</c>
/// had with MIT's KDC (<see cref="RecordedKerberosMessages" />): every message decodes to what
/// the exchange says and encodes back to the same bytes, and the encrypted parts decrypt with
/// alice's password and the session keys the exchange hands on.
/// </summary>
[TestClass]
public sealed class RecordedKerberosMessagesTests
{
    private static readonly DateTimeOffset ExchangedAt = new(2026, 9, 29, 3, 30, 43, TimeSpan.Zero);

    private static readonly KerberosEncryption Aes256 = KerberosEncryption.Create(KerberosEncryptionType.Aes256CtsHmacSha196, new SystemKerberosRandomSource());

    [TestMethod]
    public void Decode_FirstAsRequest_GivesAliceAskingForHerTgt()
    {
        KerberosKdcRequest request = KerberosKdcRequest.Decode(RecordedKerberosMessages.AsRequestWithoutPreAuthentication);

        Assert.AreEqual(KerberosMessageType.AsRequest, request.MessageType);
        CollectionAssert.AreEqual(new[] { 150, 149 }, request.PreAuthenticationData.Select(element => element.DataType).ToArray());
        Assert.AreEqual(KerberosKdcOptions.Forwardable | KerberosKdcOptions.RenewableOk, request.Body.Options);
        AssertName(request.Body.ClientName!, 1, "alice");
        Assert.AreEqual("EXAMPLE.TEST", request.Body.Realm);
        AssertName(request.Body.ServerName!, 2, "krbtgt", "EXAMPLE.TEST");
        Assert.IsNull(request.Body.From);
        Assert.AreEqual(ExchangedAt.AddDays(1), request.Body.Till);
        Assert.IsNull(request.Body.RenewTill);
        Assert.AreEqual(655942754u, request.Body.Nonce);
        CollectionAssert.AreEqual(new[] { 18, 17, 20, 19, 25, 26 }, request.Body.EncryptionTypes.ToArray());
        Assert.IsEmpty(request.Body.Addresses);
        Assert.IsNull(request.Body.EncryptedAuthorizationData);
        Assert.IsEmpty(request.Body.AdditionalTickets);
    }

    [TestMethod]
    public void Decode_PreAuthenticationRequiredError_GivesTheCodeAndTheSaltToUse()
    {
        KerberosErrorMessage error = KerberosErrorMessage.Decode(RecordedKerberosMessages.PreAuthenticationRequiredError);

        Assert.AreEqual(KerberosErrorMessage.PreAuthenticationRequired, error.ErrorCode);
        Assert.AreEqual("NEEDED_PREAUTH", error.ErrorText);
        Assert.IsNull(error.ClientTime);
        Assert.IsNull(error.ClientMicroseconds);
        Assert.AreEqual(ExchangedAt, error.ServerTime);
        Assert.AreEqual(186908, error.ServerMicroseconds);
        Assert.AreEqual("EXAMPLE.TEST", error.ClientRealm);
        AssertName(error.ClientName!, 1, "alice");
        Assert.AreEqual("EXAMPLE.TEST", error.Realm);
        AssertName(error.ServerName, 2, "krbtgt", "EXAMPLE.TEST");

        IReadOnlyList<KerberosPreAuthenticationData> methods = KerberosPreAuthenticationData.DecodeMethodData(error.ErrorData!);

        CollectionAssert.AreEqual(new[] { 136, 19, 2, 133 }, methods.Select(element => element.DataType).ToArray());
        CollectionAssert.AreEqual("MIT"u8.ToArray(), methods[3].Value);
        KerberosEncryptionTypeInfo2Entry entry = KerberosEncryptionTypeInfo2Entry.DecodeList(methods[1].Value).Single();
        Assert.AreEqual(18, entry.EncryptionType);
        Assert.AreEqual("EXAMPLE.TESTalice", entry.Salt);
        Assert.IsNull(entry.StringToKeyParameters);
    }

    [TestMethod]
    public void Decode_SecondAsRequest_CarriesAnEncryptedTimestampInAlicesKey()
    {
        KerberosKdcRequest request = KerberosKdcRequest.Decode(RecordedKerberosMessages.AsRequestWithEncryptedTimestamp);
        KerberosPreAuthenticationData timestampElement = request.PreAuthenticationData.Single(element => element.DataType == KerberosPreAuthenticationData.EncryptedTimestamp);
        KerberosEncryptedData encryptedTimestamp = KerberosEncryptedData.Decode(timestampElement.Value);

        byte[] plaintext = Aes256.Decrypt(AliceKey(), 1, encryptedTimestamp.Cipher);
        KerberosEncryptedTimestamp timestamp = KerberosEncryptedTimestamp.Decode(plaintext);

        Assert.AreEqual(487917838u, request.Body.Nonce);
        Assert.AreEqual(18, encryptedTimestamp.EncryptionType);
        Assert.IsNull(encryptedTimestamp.KeyVersionNumber);
        CollectionAssert.AreEqual(timestampElement.Value, encryptedTimestamp.Encode());
        Assert.AreEqual(ExchangedAt, timestamp.Timestamp);
        Assert.AreEqual(196017, timestamp.Microseconds);
        CollectionAssert.AreEqual(plaintext, timestamp.Encode());
    }

    [TestMethod]
    public void Decode_AsReply_DecryptsWithAlicesKeyToTheTgtSessionKey()
    {
        KerberosKdcReply reply = KerberosKdcReply.Decode(RecordedKerberosMessages.AsReply);

        byte[] plaintext = Aes256.Decrypt(AliceKey(), 3, reply.EncryptedPart.Cipher);
        using KerberosEncryptedKdcReplyPart part = KerberosEncryptedKdcReplyPart.Decode(plaintext);

        Assert.AreEqual(KerberosMessageType.AsReply, reply.MessageType);
        Assert.AreEqual(KerberosPreAuthenticationData.EncryptionTypeInfo2, reply.PreAuthenticationData.Single().DataType);
        Assert.AreEqual("EXAMPLE.TEST", reply.ClientRealm);
        AssertName(reply.ClientName, 1, "alice");
        Assert.AreEqual("EXAMPLE.TEST", reply.Ticket.Realm);
        AssertName(reply.Ticket.ServerName, 2, "krbtgt", "EXAMPLE.TEST");
        Assert.AreEqual(1u, reply.Ticket.EncryptedPart.KeyVersionNumber);

        Assert.AreEqual(KerberosMessageType.TgsReply, part.ReplyType, "MIT's KDC tags an AS-REP's encrypted part EncTGSRepPart.");
        Assert.AreEqual(18, part.Key.EncryptionType);
        Assert.AreEqual("edd4c0232495fe346c59391b6d23a2c0687e3cd37ef287eeaf844e8c627d379e", Convert.ToHexStringLower(part.Key.Value));
        Assert.AreEqual(new KerberosLastRequest(0, DateTimeOffset.UnixEpoch), part.LastRequests.Single());
        Assert.AreEqual(487917838u, part.Nonce);
        Assert.IsNull(part.KeyExpiration);
        Assert.AreEqual(KerberosTicketFlags.Forwardable | KerberosTicketFlags.Initial | KerberosTicketFlags.PreAuthenticated | KerberosTicketFlags.EncryptedPreAuthenticationReply, part.Flags);
        Assert.AreEqual(ExchangedAt, part.AuthenticationTime);
        Assert.IsNull(part.StartTime);
        Assert.AreEqual(ExchangedAt.AddDays(1), part.EndTime);
        Assert.IsNull(part.RenewUntil);
        Assert.AreEqual("EXAMPLE.TEST", part.ServerRealm);
        AssertName(part.ServerName, 2, "krbtgt", "EXAMPLE.TEST");
        Assert.IsEmpty(part.ClientAddresses);
        CollectionAssert.AreEqual(new[] { 149, 136 }, part.EncryptedPreAuthenticationData.Select(element => element.DataType).ToArray());
        CollectionAssert.AreEqual(plaintext, part.Encode());
    }

    [TestMethod]
    public void Decode_TgsRequest_CarriesAnAuthenticatorWhoseChecksumCoversTheReencodedBody()
    {
        byte[] sessionKey = TgtSessionKey();
        KerberosKdcRequest request = KerberosKdcRequest.Decode(RecordedKerberosMessages.TgsRequest);
        byte[] apRequestBytes = request.PreAuthenticationData.Single(element => element.DataType == KerberosPreAuthenticationData.TgsRequest).Value;
        KerberosApRequest apRequest = KerberosApRequest.Decode(apRequestBytes);

        byte[] plaintext = Aes256.Decrypt(sessionKey, 7, apRequest.Authenticator.Cipher);
        using KerberosAuthenticator authenticator = KerberosAuthenticator.Decode(plaintext);

        Assert.AreEqual(KerberosMessageType.TgsRequest, request.MessageType);
        Assert.AreEqual(KerberosKdcOptions.Forwardable | KerberosKdcOptions.Canonicalize, request.Body.Options);
        Assert.IsNull(request.Body.ClientName);
        AssertName(request.Body.ServerName!, 1, "HTTP", "server.example.test");
        Assert.AreEqual(1414605262u, request.Body.Nonce);
        Assert.AreEqual(KerberosApOptions.None, apRequest.Options);
        CollectionAssert.AreEqual(KerberosKdcReply.Decode(RecordedKerberosMessages.AsReply).Ticket.Encode(), apRequest.Ticket.Encode());
        CollectionAssert.AreEqual(apRequestBytes, apRequest.Encode());

        Assert.AreEqual("EXAMPLE.TEST", authenticator.ClientRealm);
        AssertName(authenticator.ClientName, 1, "alice");
        Assert.AreEqual(ExchangedAt, authenticator.ClientTime);
        Assert.AreEqual(310692, authenticator.ClientMicroseconds);
        Assert.AreEqual(18, authenticator.Subkey!.EncryptionType);
        Assert.IsNull(authenticator.SequenceNumber);
        Assert.IsEmpty(authenticator.AuthorizationData);
        Assert.AreEqual(Aes256.ChecksumType, authenticator.Checksum!.ChecksumType);
        Assert.IsTrue(Aes256.VerifyChecksum(sessionKey, 6, request.Body.Encode(), authenticator.Checksum.Value));
        CollectionAssert.AreEqual(plaintext, authenticator.Encode());
    }

    [TestMethod]
    public void Decode_TgsReply_GivesTheServiceTicket()
    {
        KerberosKdcReply reply = KerberosKdcReply.Decode(RecordedKerberosMessages.TgsReply);

        Assert.AreEqual(KerberosMessageType.TgsReply, reply.MessageType);
        Assert.AreEqual(136, reply.PreAuthenticationData.Single().DataType);
        AssertName(reply.ClientName, 1, "alice");
        AssertName(reply.Ticket.ServerName, 1, "HTTP", "server.example.test");
        Assert.AreEqual(18, reply.EncryptedPart.EncryptionType);
    }

    [TestMethod]
    [DataRow(nameof(RecordedKerberosMessages.AsRequestWithoutPreAuthentication))]
    [DataRow(nameof(RecordedKerberosMessages.AsRequestWithEncryptedTimestamp))]
    [DataRow(nameof(RecordedKerberosMessages.TgsRequest))]
    public void Encode_DecodedRequest_GivesTheRecordedBytes(string name)
    {
        byte[] recorded = Recorded(name);

        CollectionAssert.AreEqual(recorded, KerberosKdcRequest.Decode(recorded).Encode());
    }

    [TestMethod]
    [DataRow(nameof(RecordedKerberosMessages.AsReply))]
    [DataRow(nameof(RecordedKerberosMessages.TgsReply))]
    public void Encode_DecodedReply_GivesTheRecordedBytes(string name)
    {
        byte[] recorded = Recorded(name);

        CollectionAssert.AreEqual(recorded, KerberosKdcReply.Decode(recorded).Encode());
    }

    [TestMethod]
    public void Encode_DecodedError_GivesTheRecordedBytes()
    {
        byte[] recorded = RecordedKerberosMessages.PreAuthenticationRequiredError;
        KerberosErrorMessage error = KerberosErrorMessage.Decode(recorded);

        CollectionAssert.AreEqual(recorded, error.Encode());
        CollectionAssert.AreEqual(error.ErrorData, KerberosPreAuthenticationData.EncodeMethodData(KerberosPreAuthenticationData.DecodeMethodData(error.ErrorData!)));
    }

    [TestMethod]
    public void Encode_DecodedEncryptionTypeInfo2_GivesTheRecordedBytes()
    {
        byte[] recorded = KerberosKdcReply.Decode(RecordedKerberosMessages.AsReply).PreAuthenticationData.Single().Value;

        CollectionAssert.AreEqual(recorded, KerberosEncryptionTypeInfo2Entry.EncodeList(KerberosEncryptionTypeInfo2Entry.DecodeList(recorded)));
    }

    [TestMethod]
    public void Decode_CachedTicket_GivesTheTicketTheCacheNamed()
    {
        using CredentialCache cache = CredentialCacheReader.Read(RecordedKerberosFiles.AliceCredentialCache);
        byte[] recorded = cache.Credentials.Single(credential => credential.Server.Components[0] == "HTTP").Ticket;

        KerberosTicket ticket = KerberosTicket.Decode(recorded);

        Assert.AreEqual("EXAMPLE.TEST", ticket.Realm);
        AssertName(ticket.ServerName, 1, "HTTP", "server.example.test");
        Assert.AreEqual(18, ticket.EncryptedPart.EncryptionType);
        Assert.AreEqual(2u, ticket.EncryptedPart.KeyVersionNumber);
        CollectionAssert.AreEqual(recorded, ticket.Encode());
    }

    [TestMethod]
    [DataRow(nameof(RecordedKerberosMessages.AsRequestWithoutPreAuthentication), KerberosMessageType.AsRequest)]
    [DataRow(nameof(RecordedKerberosMessages.PreAuthenticationRequiredError), KerberosMessageType.Error)]
    [DataRow(nameof(RecordedKerberosMessages.AsReply), KerberosMessageType.AsReply)]
    [DataRow(nameof(RecordedKerberosMessages.TgsRequest), KerberosMessageType.TgsRequest)]
    [DataRow(nameof(RecordedKerberosMessages.TgsReply), KerberosMessageType.TgsReply)]
    public void PeekType_RecordedMessage_GivesItsType(string name, KerberosMessageType expected)
    {
        Assert.AreEqual(expected, KerberosMessage.PeekType(Recorded(name)));
    }

    private static byte[] Recorded(string name) => (byte[])typeof(RecordedKerberosMessages).GetField(name)!.GetValue(null)!;

    private static byte[] AliceKey() => Aes256.StringToKey("alicepw", Encoding.UTF8.GetBytes("EXAMPLE.TESTalice"), []);

    private static byte[] TgtSessionKey()
    {
        KerberosKdcReply reply = KerberosKdcReply.Decode(RecordedKerberosMessages.AsReply);
        using KerberosEncryptedKdcReplyPart part = KerberosEncryptedKdcReplyPart.Decode(Aes256.Decrypt(AliceKey(), 3, reply.EncryptedPart.Cipher));
        return part.Key.Value.ToArray();
    }

    private static void AssertName(KerberosPrincipalName name, int nameType, params string[] components)
    {
        Assert.AreEqual(nameType, name.NameType);
        CollectionAssert.AreEqual(components, name.Components.ToArray());
    }
}
