using System.Buffers.Binary;
using System.Text;

namespace Curl.Kerberos;

/// <summary>
/// Drives <see cref="KerberosGssContext" /> against <see cref="FakeGssAcceptor" />: the
/// initial context token and its RFC 4121 checksum, mutual authentication, delegation at each
/// <c>--delegation</c> level, and the failures of the acceptor's answer.
/// </summary>
[TestClass]
public sealed class KerberosGssContextTests
{
    /// <summary>The random bytes 01 02 03 …, so the subkey is 01…10 or 01…20 and the sequence number 0x01020304.</summary>
    internal static readonly byte[] RandomBytes = Enumerable.Range(1, 64).Select(value => (byte)value).ToArray();

    private static readonly byte[] Message = Encoding.ASCII.GetBytes("GSS-API message");

    internal static KerberosGssContext NewContext(FakeGssAcceptor acceptor, KerberosGssContextOptions? options = null, KerberosTicketFlags ticketFlags = KerberosTicketFlags.None) =>
        new(acceptor.ServiceTicket(ticketFlags), options ?? new KerberosGssContextOptions(), new FixedTimeProvider(FakeKdc.Now), new FixedKerberosRandomSource(RandomBytes));

    /// <summary>Establishes a mutually authenticated context with the acceptor.</summary>
    internal static KerberosGssContext Establish(FakeGssAcceptor acceptor)
    {
        KerberosGssContext context = NewContext(acceptor);
        acceptor.Accept(context.NextToken([]));
        Assert.IsEmpty(context.NextToken(acceptor.Reply()));
        return context;
    }

    [TestMethod]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha196)]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha196)]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha384192)]
    [DataRow(KerberosEncryptionType.Rc4Hmac)]
    public void NextToken_MutualAuthentication_SendsApRequestAndCompletesOnApReply(KerberosEncryptionType encryptionType)
    {
        FakeGssAcceptor acceptor = new(encryptionType);
        using KerberosGssContext context = NewContext(acceptor);

        byte[] initial = context.NextToken([]);

        Assert.IsFalse(context.IsCompleted);
        acceptor.Accept(initial);
        Assert.AreEqual(KerberosApOptions.MutualRequired, acceptor.Request!.Options);
        KerberosAuthenticator authenticator = acceptor.Authenticator!;
        Assert.AreEqual(FakeKdc.Realm, authenticator.ClientRealm);
        CollectionAssert.AreEqual(new[] { "alice" }, authenticator.ClientName.Components.ToArray());
        Assert.AreEqual(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero), authenticator.ClientTime);
        Assert.AreEqual(500000, authenticator.ClientMicroseconds);
        Assert.AreEqual((int)encryptionType, authenticator.Subkey!.EncryptionType);
        CollectionAssert.AreEqual(RandomBytes[..acceptor.SessionKey.Length], authenticator.Subkey.Value.ToArray());
        Assert.AreEqual(0x01020304u, authenticator.SequenceNumber);
        Assert.AreEqual(KerberosGssContext.GssChecksumType, authenticator.Checksum!.ChecksumType);
        CollectionAssert.AreEqual(Hex.Bytes("10000000 00000000000000000000000000000000 36000000"), authenticator.Checksum.Value);
        Assert.AreEqual(
            KerberosGssFlags.MutualAuthentication | KerberosGssFlags.ReplayDetection | KerberosGssFlags.Confidentiality | KerberosGssFlags.Integrity,
            context.Flags);

        Assert.IsEmpty(context.NextToken(acceptor.Reply()));
        Assert.IsTrue(context.IsCompleted);
    }

    [TestMethod]
    public void NextToken_InitialToken_IsFramedWithTheKerberosMechanism()
    {
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        using KerberosGssContext context = NewContext(acceptor);

        byte[] token = context.NextToken([]);

        Assert.AreEqual(0x60, token[0]);
        Assert.AreEqual(0x82, token[1], "the AP-REQ is longer than 255 bytes, so two length octets");
        Assert.AreEqual(token.Length - 4, BinaryPrimitives.ReadUInt16BigEndian(token.AsSpan(2)));
        CollectionAssert.AreEqual(Hex.Bytes("06092A864886F712010202 0100 6E"), token[4..18]);
    }

    [TestMethod]
    public void NextToken_WithoutMutualAuthentication_CompletesAtOnceAndNumbersBothDirectionsFromTheInitiator()
    {
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        using KerberosGssContext context = NewContext(acceptor, new KerberosGssContextOptions { RequestedFlags = KerberosGssFlags.ReplayDetection });

        acceptor.Accept(context.NextToken([]));

        Assert.IsTrue(context.IsCompleted);
        Assert.AreEqual(KerberosApOptions.None, acceptor.Request!.Options);
        Assert.AreEqual(0x34u, BinaryPrimitives.ReadUInt32LittleEndian(acceptor.Authenticator!.Checksum!.Value.AsSpan(20)));
        CollectionAssert.AreEqual(Message, context.Unwrap(acceptor.Rfc4121Wrap(Message, 0x01020304, encrypt: true)).Message);
    }

    [TestMethod]
    [DataRow(KerberosDelegation.None, KerberosTicketFlags.OkAsDelegate, true, 0x36u)]
    [DataRow(KerberosDelegation.Policy, KerberosTicketFlags.None, true, 0x36u)]
    [DataRow(KerberosDelegation.Policy, KerberosTicketFlags.OkAsDelegate, true, 0x37u)]
    [DataRow(KerberosDelegation.Always, KerberosTicketFlags.None, true, 0x37u)]
    [DataRow(KerberosDelegation.Always, KerberosTicketFlags.None, false, 0x36u)]
    public void NextToken_Delegation_SetsTheChecksumFlagsForEachLevel(KerberosDelegation delegation, KerberosTicketFlags ticketFlags, bool forwardable, uint expectedFlags)
    {
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        using KerberosCredential forwarded = ForwardedTicketGrantingTicket();
        using KerberosGssContext context = NewContext(
            acceptor,
            new KerberosGssContextOptions { Delegation = delegation, ForwardedTicketGrantingTicket = forwardable ? forwarded : null },
            ticketFlags);

        acceptor.Accept(context.NextToken([]));

        byte[] checksum = acceptor.Authenticator!.Checksum!.Value;
        Assert.AreEqual(expectedFlags, BinaryPrimitives.ReadUInt32LittleEndian(checksum.AsSpan(20)));
        Assert.AreEqual(expectedFlags, (uint)context.Flags);
        Assert.AreEqual((expectedFlags & 1) == 0 ? 24 : 28 + BinaryPrimitives.ReadUInt16LittleEndian(checksum.AsSpan(26)), checksum.Length);
    }

    [TestMethod]
    public void NextToken_Delegation_CarriesTheForwardedTicketInAKrbCredInTheSessionKey()
    {
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        using KerberosCredential forwarded = ForwardedTicketGrantingTicket();
        using KerberosGssContext context = NewContext(acceptor, new KerberosGssContextOptions { Delegation = KerberosDelegation.Always, ForwardedTicketGrantingTicket = forwarded });

        acceptor.Accept(context.NextToken([]));

        byte[] checksum = acceptor.Authenticator!.Checksum!.Value;
        Assert.AreEqual(1, BinaryPrimitives.ReadUInt16LittleEndian(checksum.AsSpan(24)), "DlgOpt");
        KerberosCredentialMessage credential = KerberosCredentialMessage.Decode(checksum.AsMemory(28));
        CollectionAssert.AreEqual(forwarded.Ticket.Encode(), credential.Tickets.Single().Encode());
        Assert.AreEqual(18, credential.EncryptedPart.EncryptionType);
        using KerberosEncryptedCredentialPart part = KerberosEncryptedCredentialPart.Decode(acceptor.Encryption.Decrypt(acceptor.SessionKey, 14, credential.EncryptedPart.Cipher));
        KerberosCredentialInfo info = part.Credentials.Single();
        CollectionAssert.AreEqual(forwarded.SessionKey.Value.ToArray(), info.Key.Value.ToArray());
        Assert.AreEqual(FakeKdc.Realm, info.ClientRealm);
        CollectionAssert.AreEqual(new[] { "alice" }, info.ClientName.Components.ToArray());
        Assert.AreEqual(KerberosTicketFlags.Forwardable | KerberosTicketFlags.Forwarded, info.Flags);
        Assert.AreEqual(forwarded.AuthenticationTime, info.AuthenticationTime);
        Assert.AreEqual(forwarded.EndTime, info.EndTime);
        Assert.IsNull(info.StartTime);
        Assert.IsNull(info.RenewUntil);
        Assert.AreEqual(FakeKdc.Realm, info.ServerRealm);
        CollectionAssert.AreEqual(new[] { "krbtgt", FakeKdc.Realm }, info.ServerName.Components.ToArray());
        Assert.AreEqual(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero), part.Timestamp);
        Assert.AreEqual(500000, part.Microseconds);
    }

    [TestMethod]
    public void NextToken_AcceptorSubkeyAndNoSequenceNumber_UsesTheSubkeyAndNumbersTheAcceptorFromZero()
    {
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196) { AcceptorSubkey = Enumerable.Repeat((byte)0x33, 32).ToArray(), AcceptorSequence = null };
        using KerberosGssContext context = Establish(acceptor);

        CollectionAssert.AreEqual(Message, context.Unwrap(acceptor.Rfc4121Wrap(Message, 0, encrypt: true)).Message);
        Assert.AreEqual(0x01020304ul, acceptor.Rfc4121VerifyMic(Message, context.GetMic(Message)));
    }

    [TestMethod]
    public void NextToken_KrbError_ThrowsAcceptorErrorWithItsCode()
    {
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        using KerberosGssContext context = NewContext(acceptor);
        context.NextToken([]);
        byte[] error = FakeGssAcceptor.Frame([0x03, 0x00, .. FakeKdc.Error(41)]);

        KerberosGssException exception = Assert.ThrowsExactly<KerberosGssException>(() => context.NextToken(error));

        Assert.AreEqual(KerberosGssError.AcceptorError, exception.Error);
        Assert.AreEqual(41, exception.KerberosErrorCode);
    }

    [TestMethod]
    public void NextToken_AcceptorAnswers_FailWithTypedErrors()
    {
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        byte[] otherKey = Enumerable.Repeat((byte)0x44, 32).ToArray();

        AssertReplyFails(acceptor, _ => FakeGssAcceptor.Frame([0x03, 0x00, 0x30, 0x00]), KerberosGssError.MalformedToken);
        AssertReplyFails(acceptor, _ => FakeGssAcceptor.Frame([0x04, 0x04, .. acceptor.Reply()]), KerberosGssError.MalformedToken);
        AssertReplyFails(acceptor, _ => FakeGssAcceptor.ReplyToken([0x30, 0x00]), KerberosGssError.MalformedToken);
        AssertReplyFails(acceptor, _ => acceptor.Reply(otherKey), KerberosGssError.MutualAuthenticationFailed);
        AssertReplyFails(acceptor, _ => [0x30, 0x00], KerberosGssError.MalformedToken);
        AssertReplyFails(acceptor, EncryptedGarbageReply, KerberosGssError.MalformedToken);
        acceptor.EchoedMicrosecondsOffset = 1;
        AssertReplyFails(acceptor, _ => acceptor.Reply(), KerberosGssError.MutualAuthenticationFailed);
    }

    [TestMethod]
    public void NextToken_ApReplyEchoingAnotherSecond_FailsMutualAuthentication()
    {
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        AssertReplyFails(
            acceptor,
            authenticator =>
            {
                KerberosEncryptedApReplyPart part = new() { ClientTime = authenticator.ClientTime.AddSeconds(1), ClientMicroseconds = authenticator.ClientMicroseconds };
                byte[] cipher = acceptor.Encryption.Encrypt(acceptor.SessionKey, 12, part.Encode());
                return FakeGssAcceptor.ReplyToken(new KerberosApReply(new KerberosEncryptedData(18, null, cipher)).Encode());
            },
            KerberosGssError.MutualAuthenticationFailed);
    }

    [TestMethod]
    public void Methods_UsedOutOfTurn_Throw()
    {
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        using KerberosGssContext context = NewContext(acceptor);

        Assert.ThrowsExactly<InvalidOperationException>(() => context.Wrap(Message, encrypt: true));
        Assert.ThrowsExactly<InvalidOperationException>(() => context.Unwrap(Message));
        Assert.ThrowsExactly<InvalidOperationException>(() => context.GetMic(Message));
        Assert.ThrowsExactly<InvalidOperationException>(() => context.VerifyMic(Message, Message));
        acceptor.Accept(context.NextToken([]));
        context.NextToken(acceptor.Reply());
        Assert.ThrowsExactly<InvalidOperationException>(() => context.NextToken([]));
    }

    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        TimeProvider time = new FixedTimeProvider(FakeKdc.Now);
        IKerberosRandomSource random = new FixedKerberosRandomSource(RandomBytes);

        Assert.ThrowsExactly<ArgumentNullException>(() => new KerberosGssContext(null!, new KerberosGssContextOptions(), time, random));
        Assert.ThrowsExactly<ArgumentNullException>(() => new KerberosGssContext(acceptor.ServiceTicket(), null!, time, random));
    }

    [TestMethod]
    public void Dispose_BeforeTheFirstToken_DoesNothingToThrowOn()
    {
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        KerberosGssContext context = NewContext(acceptor);

        context.Dispose();

        Assert.AreEqual(KerberosGssFlags.None, context.Flags);
    }

    [TestMethod]
    public void Exception_WithoutKerberosErrorCode_HasNone()
    {
        KerberosGssException exception = new(KerberosGssError.IntegrityCheckFailed);

        Assert.IsNull(exception.KerberosErrorCode);
        Assert.AreEqual("GSS-API Kerberos failed: IntegrityCheckFailed.", exception.Message);
    }

    private static KerberosCredential ForwardedTicketGrantingTicket() => new()
    {
        Client = FakeKdc.Alice,
        Server = KerberosKdcClient.TicketGrantingServer(FakeKdc.Realm),
        Ticket = FakeKdc.TicketGrantingTicket,
        SessionKey = new KerberosKey(18, FakeKdc.TicketGrantingSessionKey.ToArray()),
        Flags = KerberosTicketFlags.Forwardable | KerberosTicketFlags.Forwarded,
        AuthenticationTime = new DateTimeOffset(2026, 9, 28, 11, 0, 0, TimeSpan.Zero),
        EndTime = new DateTimeOffset(2026, 9, 28, 21, 0, 0, TimeSpan.Zero),
    };

    private static void AssertReplyFails(FakeGssAcceptor acceptor, Func<KerberosAuthenticator, byte[]> reply, KerberosGssError expected)
    {
        using KerberosGssContext context = NewContext(acceptor);
        acceptor.Accept(context.NextToken([]));

        KerberosGssException exception = Assert.ThrowsExactly<KerberosGssException>(() => context.NextToken(reply(acceptor.Authenticator!)));

        Assert.AreEqual(expected, exception.Error);
        Assert.IsFalse(context.IsCompleted);
    }

    private static byte[] EncryptedGarbageReply(KerberosAuthenticator authenticator)
    {
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        byte[] cipher = acceptor.Encryption.Encrypt(acceptor.SessionKey, 12, [0x30, 0x00]);
        return FakeGssAcceptor.ReplyToken(new KerberosApReply(new KerberosEncryptedData(18, null, cipher)).Encode());
    }
}
