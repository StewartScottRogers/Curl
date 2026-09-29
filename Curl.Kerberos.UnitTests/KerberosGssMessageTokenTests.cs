using System.Text;

namespace Curl.Kerberos;

/// <summary>
/// Round-trips Wrap and MIC tokens between an established <see cref="KerberosGssContext" />
/// and <see cref="FakeGssAcceptor" /> in both directions, RFC 4121's for AES and RFC 4757's
/// for <c>rc4-hmac</c>, and pins the refusal of replayed, reordered, reflected, altered and
/// malformed tokens.
/// </summary>
[TestClass]
public sealed class KerberosGssMessageTokenTests
{
    private const uint InitiatorSequence = 0x01020304;
    private const uint AcceptorSequence = 0x00AB0000;

    private static readonly byte[] Message = Encoding.ASCII.GetBytes("GSS-API message");

    private static readonly string MessageHex = Convert.ToHexString(Message);

    [TestMethod]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha196, false)]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha196, true)]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha256128, false)]
    public void Rfc4121_InitiatorTokens_AreReadByTheAcceptorInSequence(KerberosEncryptionType encryptionType, bool acceptorSubkey)
    {
        FakeGssAcceptor acceptor = Acceptor(encryptionType, acceptorSubkey);
        using KerberosGssContext context = KerberosGssContextTests.Establish(acceptor);

        Assert.AreEqual((MessageHex, (ulong)InitiatorSequence, true), Flatten(acceptor.Rfc4121Unwrap(context.Wrap(Message, encrypt: true))));
        Assert.AreEqual((MessageHex, (ulong)InitiatorSequence + 1, false), Flatten(acceptor.Rfc4121Unwrap(context.Wrap(Message, encrypt: false))));
        Assert.AreEqual(InitiatorSequence + 2ul, acceptor.Rfc4121VerifyMic(Message, context.GetMic(Message)));
        Assert.AreEqual(0ul + InitiatorSequence + 3, acceptor.Rfc4121Unwrap(context.Wrap([], encrypt: true)).Sequence);
    }

    [TestMethod]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha196, false)]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha196, true)]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha384192, false)]
    public void Rfc4121_AcceptorTokens_AreReadByTheInitiatorInSequence(KerberosEncryptionType encryptionType, bool acceptorSubkey)
    {
        FakeGssAcceptor acceptor = Acceptor(encryptionType, acceptorSubkey);
        using KerberosGssContext context = KerberosGssContextTests.Establish(acceptor);

        KerberosGssUnwrapped sealedMessage = context.Unwrap(acceptor.Rfc4121Wrap(Message, AcceptorSequence, encrypt: true));
        KerberosGssUnwrapped signedMessage = context.Unwrap(acceptor.Rfc4121Wrap(Message, AcceptorSequence + 1, encrypt: false));
        context.VerifyMic(Message, acceptor.Rfc4121Mic(Message, AcceptorSequence + 2));
        KerberosGssUnwrapped rotatedSealed = context.Unwrap(acceptor.Rfc4121Wrap(Message, AcceptorSequence + 3, encrypt: true, rotation: 28));
        KerberosGssUnwrapped rotatedSigned = context.Unwrap(acceptor.Rfc4121Wrap(Message, AcceptorSequence + 4, encrypt: false, rotation: 1000));

        CollectionAssert.AreEqual(Message, sealedMessage.Message);
        Assert.IsTrue(sealedMessage.Encrypted);
        CollectionAssert.AreEqual(Message, signedMessage.Message);
        Assert.IsFalse(signedMessage.Encrypted);
        CollectionAssert.AreEqual(Message, rotatedSealed.Message);
        CollectionAssert.AreEqual(Message, rotatedSigned.Message);
    }

    [TestMethod]
    public void Rfc4121_ReplayedOrSkippedToken_ThrowsBadSequenceNumber()
    {
        FakeGssAcceptor acceptor = Acceptor(KerberosEncryptionType.Aes256CtsHmacSha196, false);
        using KerberosGssContext context = KerberosGssContextTests.Establish(acceptor);
        byte[] first = acceptor.Rfc4121Wrap(Message, AcceptorSequence, encrypt: true);
        context.Unwrap(first);

        AssertFails(KerberosGssError.BadSequenceNumber, () => context.Unwrap(first));
        AssertFails(KerberosGssError.BadSequenceNumber, () => context.VerifyMic(Message, acceptor.Rfc4121Mic(Message, AcceptorSequence + 2)));
        context.VerifyMic(Message, acceptor.Rfc4121Mic(Message, AcceptorSequence + 1));
    }

    [TestMethod]
    public void Rfc4121_AlteredTokens_ThrowIntegrityCheckFailed()
    {
        FakeGssAcceptor acceptor = Acceptor(KerberosEncryptionType.Aes256CtsHmacSha196, false);
        using KerberosGssContext context = KerberosGssContextTests.Establish(acceptor);

        AssertFails(KerberosGssError.IntegrityCheckFailed, () => context.Unwrap(Flip(acceptor.Rfc4121Wrap(Message, AcceptorSequence, encrypt: true), 20)));
        AssertFails(KerberosGssError.IntegrityCheckFailed, () => context.Unwrap(Flip(acceptor.Rfc4121Wrap(Message, AcceptorSequence, encrypt: true), 15)));
        AssertFails(KerberosGssError.IntegrityCheckFailed, () => context.Unwrap(Flip(acceptor.Rfc4121Wrap(Message, AcceptorSequence, encrypt: false), 17)));
        AssertFails(KerberosGssError.IntegrityCheckFailed, () => context.VerifyMic(Flip(Message, 0), acceptor.Rfc4121Mic(Message, AcceptorSequence)));
    }

    [TestMethod]
    public void Rfc4121_MalformedTokens_ThrowMalformedToken()
    {
        FakeGssAcceptor acceptor = Acceptor(KerberosEncryptionType.Aes256CtsHmacSha196, false);
        using KerberosGssContext context = KerberosGssContextTests.Establish(acceptor);
        byte[] sealedToken = acceptor.Rfc4121Wrap(Message, AcceptorSequence, encrypt: true);
        byte[] mic = acceptor.Rfc4121Mic(Message, AcceptorSequence);

        AssertFails(KerberosGssError.MalformedToken, () => context.Unwrap(sealedToken[..16]));
        AssertFails(KerberosGssError.MalformedToken, () => context.Unwrap(Replace(sealedToken, 1, 0x05)));
        AssertFails(KerberosGssError.MalformedToken, () => context.Unwrap(Replace(sealedToken, 2, 0x02)), "reflected: SentByAcceptor clear");
        AssertFails(KerberosGssError.MalformedToken, () => context.Unwrap(Replace(sealedToken, 3, 0x00)));
        AssertFails(KerberosGssError.MalformedToken, () => context.Unwrap(acceptor.Rfc4121Wrap(Message, AcceptorSequence, encrypt: true, extraCount: 0xFFFF)));
        AssertFails(KerberosGssError.MalformedToken, () => context.Unwrap(acceptor.Rfc4121Wrap(Message, AcceptorSequence, encrypt: false, extraCount: 11)));
        AssertFails(KerberosGssError.MalformedToken, () => context.Unwrap(acceptor.Rfc4121Wrap(Message, AcceptorSequence, encrypt: false)[..20]));
        AssertFails(KerberosGssError.MalformedToken, () => context.VerifyMic(Message, mic[..^1]));
        AssertFails(KerberosGssError.MalformedToken, () => context.VerifyMic(Message, Replace(mic, 4, 0x00)));
    }

    [TestMethod]
    public void Rc4Hmac_InitiatorTokens_AreReadByTheAcceptorInSequence()
    {
        FakeGssAcceptor acceptor = Acceptor(KerberosEncryptionType.Rc4Hmac, false);
        using KerberosGssContext context = KerberosGssContextTests.Establish(acceptor);

        Assert.AreEqual((MessageHex, InitiatorSequence, true), Flatten(acceptor.Rc4Unwrap(context.Wrap(Message, encrypt: true))));
        Assert.AreEqual((MessageHex, InitiatorSequence + 1, false), Flatten(acceptor.Rc4Unwrap(context.Wrap(Message, encrypt: false))));
        Assert.AreEqual(InitiatorSequence + 2, acceptor.Rc4VerifyMic(Message, context.GetMic(Message)));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Rc4Hmac_AcceptorTokens_AreReadByTheInitiatorInSequence(bool acceptorSubkey)
    {
        FakeGssAcceptor acceptor = Acceptor(KerberosEncryptionType.Rc4Hmac, acceptorSubkey);
        using KerberosGssContext context = KerberosGssContextTests.Establish(acceptor);
        byte[] large = Enumerable.Range(0, 300).Select(value => (byte)value).ToArray();

        KerberosGssUnwrapped sealedMessage = context.Unwrap(acceptor.Rc4Wrap(Message, AcceptorSequence, encrypt: true));
        KerberosGssUnwrapped signedMessage = context.Unwrap(acceptor.Rc4Wrap(Message, AcceptorSequence + 1, encrypt: false, padding: [2, 2]));
        context.VerifyMic(Message, acceptor.Rc4Mic(Message, AcceptorSequence + 2));
        KerberosGssUnwrapped largeMessage = context.Unwrap(acceptor.Rc4Wrap(large, AcceptorSequence + 3, encrypt: true));

        CollectionAssert.AreEqual(Message, sealedMessage.Message);
        Assert.IsTrue(sealedMessage.Encrypted);
        CollectionAssert.AreEqual(Message, signedMessage.Message);
        Assert.IsFalse(signedMessage.Encrypted);
        CollectionAssert.AreEqual(large, largeMessage.Message);
        CollectionAssert.AreEqual(large, acceptor.Rc4Unwrap(context.Wrap(large, encrypt: true)).Message);
    }

    [TestMethod]
    public void Rc4Hmac_ReplayedReorderedOrReflectedToken_ThrowsBadSequenceNumber()
    {
        FakeGssAcceptor acceptor = Acceptor(KerberosEncryptionType.Rc4Hmac, false);
        using KerberosGssContext context = KerberosGssContextTests.Establish(acceptor);
        byte[] first = acceptor.Rc4Wrap(Message, AcceptorSequence, encrypt: true);
        context.Unwrap(first);

        AssertFails(KerberosGssError.BadSequenceNumber, () => context.Unwrap(first));
        AssertFails(KerberosGssError.BadSequenceNumber, () => context.VerifyMic(Message, acceptor.Rc4Mic(Message, AcceptorSequence + 5)));
        AssertFails(KerberosGssError.BadSequenceNumber, () => context.VerifyMic(Message, acceptor.Rc4Mic(Message, AcceptorSequence + 1, direction: 0x00)));
        context.VerifyMic(Message, acceptor.Rc4Mic(Message, AcceptorSequence + 1));
    }

    [TestMethod]
    public void Rc4Hmac_AlteredTokens_ThrowIntegrityCheckFailed()
    {
        FakeGssAcceptor acceptor = Acceptor(KerberosEncryptionType.Rc4Hmac, false);
        using KerberosGssContext context = KerberosGssContextTests.Establish(acceptor);
        byte[] sealedToken = acceptor.Rc4Wrap(Message, AcceptorSequence, encrypt: true);
        byte[] mic = acceptor.Rc4Mic(Message, AcceptorSequence);

        AssertFails(KerberosGssError.IntegrityCheckFailed, () => context.Unwrap(Flip(sealedToken, sealedToken.Length - 3)));
        AssertFails(KerberosGssError.IntegrityCheckFailed, () => context.Unwrap(Flip(sealedToken, 40)));
        AssertFails(KerberosGssError.IntegrityCheckFailed, () => context.VerifyMic(Flip(Message, 0), mic));
        AssertFails(KerberosGssError.IntegrityCheckFailed, () => context.VerifyMic(Message, Flip(mic, mic.Length - 1)));
    }

    [TestMethod]
    public void Rc4Hmac_MalformedTokens_ThrowMalformedToken()
    {
        FakeGssAcceptor acceptor = Acceptor(KerberosEncryptionType.Rc4Hmac, false);
        using KerberosGssContext context = KerberosGssContextTests.Establish(acceptor);
        byte[] signedInner = FakeGssAcceptor.Unframe(acceptor.Rc4Wrap(Message, AcceptorSequence, encrypt: false));
        byte[] micInner = FakeGssAcceptor.Unframe(acceptor.Rc4Mic(Message, AcceptorSequence));

        AssertFails(KerberosGssError.MalformedToken, () => context.Unwrap(FakeGssAcceptor.Frame(signedInner[..32])));
        AssertFails(KerberosGssError.MalformedToken, () => context.Unwrap(FakeGssAcceptor.Frame(Replace(signedInner, 4, 0x20))));
        AssertFails(KerberosGssError.MalformedToken, () => context.Unwrap(acceptor.Rc4Wrap(Message, AcceptorSequence, encrypt: false, padding: [0])));
        AssertFails(KerberosGssError.MalformedToken, () => context.Unwrap(acceptor.Rc4Wrap([], AcceptorSequence, encrypt: false, padding: [9])));
        AssertFails(KerberosGssError.MalformedToken, () => context.VerifyMic(Message, FakeGssAcceptor.Frame(micInner[..^1])));
        AssertFails(KerberosGssError.MalformedToken, () => context.VerifyMic(Message, FakeGssAcceptor.Frame(Replace(micInner, 2, 0x12))));
    }

    [TestMethod]
    public void Rc4Hmac_BadFraming_ThrowsMalformedToken()
    {
        FakeGssAcceptor acceptor = Acceptor(KerberosEncryptionType.Rc4Hmac, false);
        using KerberosGssContext context = KerberosGssContextTests.Establish(acceptor);
        byte[] mic = acceptor.Rc4Mic(Message, AcceptorSequence);

        AssertFails(KerberosGssError.MalformedToken, () => context.VerifyMic(Message, []));
        AssertFails(KerberosGssError.MalformedToken, () => context.VerifyMic(Message, Replace(mic, 0, 0x30)));
        AssertFails(KerberosGssError.MalformedToken, () => context.VerifyMic(Message, [.. mic, 0x00]));
        AssertFails(KerberosGssError.MalformedToken, () => context.VerifyMic(Message, [0x60, 0x0C, .. FakeGssAcceptor.MechanismOid, 0x01]));
        AssertFails(KerberosGssError.MalformedToken, () => context.VerifyMic(Message, Replace(mic, 12, 0x03)));
        context.VerifyMic(Message, mic);
    }

    private static FakeGssAcceptor Acceptor(KerberosEncryptionType encryptionType, bool acceptorSubkey)
    {
        FakeGssAcceptor acceptor = new(encryptionType);
        acceptor.AcceptorSubkey = acceptorSubkey ? Enumerable.Repeat((byte)0x33, acceptor.SessionKey.Length).ToArray() : null;
        return acceptor;
    }

    private static (string Message, T Sequence, bool Encrypted) Flatten<T>((byte[] Message, T Sequence, bool Encrypted) unwrapped) =>
        (Convert.ToHexString(unwrapped.Message), unwrapped.Sequence, unwrapped.Encrypted);

    private static byte[] Flip(byte[] bytes, int index) => Replace(bytes, index, (byte)(bytes[index] ^ 0x01));

    private static byte[] Replace(byte[] bytes, int index, byte value)
    {
        byte[] copy = bytes.ToArray();
        copy[index] = value;
        return copy;
    }

    private static void AssertFails(KerberosGssError expected, Action action, string message = "")
    {
        KerberosGssException exception = Assert.ThrowsExactly<KerberosGssException>(action, message);
        Assert.AreEqual(expected, exception.Error, message);
    }
}
