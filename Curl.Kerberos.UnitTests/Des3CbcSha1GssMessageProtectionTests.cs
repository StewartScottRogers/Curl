using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Curl.Kerberos;

/// <summary>
/// Checks the RFC 1964 DES3 Wrap and MIC tokens a <c>des3-cbc-sha1</c> context key gets
/// against tokens made here step by step as MIT's <c>k5seal.c</c> (<c>make_seal_token_v1</c>)
/// and <c>util_seqnum.c</c> (<c>kg_make_seq_num</c>) make them, with the context key as
/// <c>des3-cbc-raw</c> for the sequence number and the data (<c>kg_setup_keys</c>), and pins
/// the refusal of replayed, reflected, altered and malformed tokens.
/// </summary>
[TestClass]
public sealed class Des3CbcSha1GssMessageProtectionTests
{
    private const uint InitiatorSequence = 0x01020304;
    private const uint AcceptorSequence = 0x00AB0000;
    private const byte InitiatorDirection = 0x00;
    private const byte AcceptorDirection = 0xFF;

    // The des3-cbc-sha1 key of MIT's t_cksums.c.
    private static readonly byte[] Key = Hex.Bytes("7A 25 DF 89 92 29 6D CE DA 0E 13 5B C4 04 6E 23 75 B3 C1 4C 98 FB C1 62");

    private static readonly byte[] Confounder = Hex.Bytes("5A 5A 5A 5A 5A 5A 5A 5A");

    private static readonly byte[] Message = Encoding.ASCII.GetBytes("GSS-API message");

    private static readonly byte[] MicHeader = [0x01, 0x01, 0x04, 0x00, 0xFF, 0xFF, 0xFF, 0xFF];
    private static readonly byte[] SealedWrapHeader = [0x02, 0x01, 0x04, 0x00, 0x02, 0x00, 0xFF, 0xFF];
    private static readonly byte[] SignedWrapHeader = [0x02, 0x01, 0x04, 0x00, 0xFF, 0xFF, 0xFF, 0xFF];

    [TestMethod]
    public void Create_Des3CbcSha1Key_GivesRfc1964Des3Tokens()
    {
        using KerberosGssMessageProtection protection = Protection();

        Assert.IsInstanceOfType<Des3CbcSha1GssMessageProtection>(protection);
    }

    [TestMethod]
    public void InitiatorTokens_MatchMitsTokensInSequence()
    {
        using KerberosGssMessageProtection protection = Protection();

        CollectionAssert.AreEqual(MitWrap(Plain(Message), InitiatorSequence, InitiatorDirection, encrypt: true), protection.Wrap(Message, encrypt: true));
        CollectionAssert.AreEqual(MitWrap(Plain(Message), InitiatorSequence + 1, InitiatorDirection, encrypt: false), protection.Wrap(Message, encrypt: false));
        CollectionAssert.AreEqual(MitMic(Message, InitiatorSequence + 2, InitiatorDirection), protection.GetMic(Message));
        CollectionAssert.AreEqual(MitWrap(Plain([]), InitiatorSequence + 3, InitiatorDirection, encrypt: true), protection.Wrap([], encrypt: true));
    }

    // Derived from MIT's make_seal_token_v1 for the key above, the message "GSS-API message"
    // and initiator sequence number 0x01020304: framing, header 0101 0400 FFFF FFFF, the
    // encrypted sequence number, then the 20-byte checksum.
    [TestMethod]
    public void GetMic_PinnedVector_MatchesMitsToken()
    {
        using KerberosGssMessageProtection protection = Protection();

        Assert.AreEqual(
            "60 2F 06 09 2A 86 48 86 F7 12 01 02 02 01 01 04 00 FF FF FF FF 21 33 E3 23 AC 11 6D 78 AD 8A A1 FA 73 6B 86 EA 97 12 47 3C EC 71 65 8A A7 E0 1A A7",
            string.Join(' ', Convert.ToHexString(protection.GetMic(Message)).Chunk(2).Select(pair => new string(pair))));
    }

    [TestMethod]
    public void AcceptorTokens_AreReadInSequence()
    {
        using KerberosGssMessageProtection protection = Protection();
        byte[] block = Encoding.ASCII.GetBytes("8 bytes!");

        KerberosGssUnwrapped sealedMessage = protection.Unwrap(MitWrap(Plain(Message), AcceptorSequence, AcceptorDirection, encrypt: true));
        KerberosGssUnwrapped signedMessage = protection.Unwrap(MitWrap(Plain(Message), AcceptorSequence + 1, AcceptorDirection, encrypt: false));
        protection.VerifyMic(Message, MitMic(Message, AcceptorSequence + 2, AcceptorDirection));
        KerberosGssUnwrapped blockMessage = protection.Unwrap(MitWrap(Plain(block), AcceptorSequence + 3, AcceptorDirection, encrypt: true));
        KerberosGssUnwrapped emptyMessage = protection.Unwrap(MitWrap(Plain([]), AcceptorSequence + 4, AcceptorDirection, encrypt: false));

        Assert.AreEqual((Convert.ToHexString(Message), true), (Convert.ToHexString(sealedMessage.Message), sealedMessage.Encrypted));
        Assert.AreEqual((Convert.ToHexString(Message), false), (Convert.ToHexString(signedMessage.Message), signedMessage.Encrypted));
        CollectionAssert.AreEqual(block, blockMessage.Message);
        Assert.IsEmpty(emptyMessage.Message);
    }

    [TestMethod]
    public void ReplayedSkippedOrReflectedToken_ThrowsBadSequenceNumber()
    {
        using KerberosGssMessageProtection protection = Protection();
        byte[] first = MitWrap(Plain(Message), AcceptorSequence, AcceptorDirection, encrypt: true);
        protection.Unwrap(first);

        AssertFails(KerberosGssError.BadSequenceNumber, () => protection.Unwrap(first));
        AssertFails(KerberosGssError.BadSequenceNumber, () => protection.VerifyMic(Message, MitMic(Message, AcceptorSequence + 5, AcceptorDirection)));
        AssertFails(KerberosGssError.BadSequenceNumber, () => protection.VerifyMic(Message, MitMic(Message, AcceptorSequence + 1, InitiatorDirection)), "reflected");
        protection.VerifyMic(Message, MitMic(Message, AcceptorSequence + 1, AcceptorDirection));
    }

    [TestMethod]
    public void AlteredTokens_ThrowIntegrityCheckFailed()
    {
        using KerberosGssMessageProtection protection = Protection();
        byte[] sealedToken = MitWrap(Plain(Message), AcceptorSequence, AcceptorDirection, encrypt: true);
        byte[] signedToken = MitWrap(Plain(Message), AcceptorSequence, AcceptorDirection, encrypt: false);
        byte[] mic = MitMic(Message, AcceptorSequence, AcceptorDirection);

        AssertFails(KerberosGssError.IntegrityCheckFailed, () => protection.Unwrap(Flip(sealedToken, sealedToken.Length - 12)));
        AssertFails(KerberosGssError.IntegrityCheckFailed, () => protection.Unwrap(Flip(signedToken, signedToken.Length - 12)));
        AssertFails(KerberosGssError.IntegrityCheckFailed, () => protection.Unwrap(Flip(sealedToken, 40)));
        AssertFails(KerberosGssError.IntegrityCheckFailed, () => protection.VerifyMic(Flip(Message, 0), mic));
        AssertFails(KerberosGssError.IntegrityCheckFailed, () => protection.VerifyMic(Message, Flip(mic, mic.Length - 1)));
    }

    [TestMethod]
    public void MalformedTokens_ThrowMalformedToken()
    {
        using KerberosGssMessageProtection protection = Protection();
        byte[] signedInner = FakeGssAcceptor.Unframe(MitWrap(Plain(Message), AcceptorSequence, AcceptorDirection, encrypt: false));
        byte[] micInner = FakeGssAcceptor.Unframe(MitMic(Message, AcceptorSequence, AcceptorDirection));

        AssertFails(KerberosGssError.MalformedToken, () => protection.Unwrap(FakeGssAcceptor.Frame(signedInner[..44])), "shorter than a confounder and a padding block");
        AssertFails(KerberosGssError.MalformedToken, () => protection.Unwrap(FakeGssAcceptor.Frame(signedInner[..^1])), "not whole blocks");
        AssertFails(KerberosGssError.MalformedToken, () => protection.Unwrap(FakeGssAcceptor.Frame(Replace(signedInner, 2, 0x11))), "rc4-hmac's SGN_ALG");
        AssertFails(KerberosGssError.MalformedToken, () => protection.Unwrap(MitWrap([.. Confounder, .. Message, 0x00], AcceptorSequence, AcceptorDirection, encrypt: false)), "padding 0");
        AssertFails(KerberosGssError.MalformedToken, () => protection.Unwrap(MitWrap([.. Confounder, .. Message, 0x09], AcceptorSequence, AcceptorDirection, encrypt: false)), "padding 9");
        AssertFails(KerberosGssError.MalformedToken, () => protection.VerifyMic(Message, FakeGssAcceptor.Frame(micInner[..^1])));
        AssertFails(KerberosGssError.MalformedToken, () => protection.VerifyMic(Message, FakeGssAcceptor.Frame(Replace(micInner, 4, 0x02))));
    }

    private static KerberosGssMessageProtection Protection() =>
        KerberosGssMessageProtection.Create(
            new KerberosKey((int)KerberosEncryptionType.Des3CbcSha1, Key.ToArray()),
            acceptorSubkey: false,
            InitiatorSequence,
            AcceptorSequence,
            new FixedKerberosRandomSource(Confounder));

    /// <summary>The confounder, the message and 1 to 8 bytes of padding each holding the padding's length.</summary>
    private static byte[] Plain(byte[] message)
    {
        int padding = 8 - (message.Length % 8);
        return [.. Confounder, .. message, .. Enumerable.Repeat((byte)padding, padding)];
    }

    private static byte[] MitMic(byte[] message, uint sequence, byte direction) => MitToken(MicHeader, sequence, direction, message, []);

    private static byte[] MitWrap(byte[] plain, uint sequence, byte direction, bool encrypt) =>
        MitToken(encrypt ? SealedWrapHeader : SignedWrapHeader, sequence, direction, plain, encrypt ? Cbc(plain, new byte[8]) : plain);

    /// <summary>
    /// The header, the sequence number (four little-endian bytes and four direction bytes)
    /// encrypted with the checksum's first block as IV, the <c>hmac-sha1-des3-kd</c> checksum
    /// for usage 23 (<c>KG_USAGE_SIGN</c>) of the header and the signed bytes, then the body.
    /// </summary>
    private static byte[] MitToken(byte[] header, uint sequence, byte direction, byte[] signed, byte[] body)
    {
        byte[] checksumKey = Des3CbcSha1KerberosEncryption.DeriveKey(Key, [0, 0, 0, 23, 0x99]);
        byte[] checksummed = [.. header, .. signed];
        byte[] checksum = HMACSHA1.HashData(checksumKey, checksummed);
        byte[] plainSequence = [0, 0, 0, 0, direction, direction, direction, direction];
        BinaryPrimitives.WriteUInt32LittleEndian(plainSequence, sequence);
        return FakeGssAcceptor.Frame([.. header, .. Cbc(plainSequence, checksum[..8]), .. checksum, .. body]);
    }

    private static byte[] Cbc(byte[] data, byte[] initializationVector)
    {
        using TripleDES tripleDes = TripleDES.Create();
        tripleDes.Key = Key;
        return tripleDes.EncryptCbc(data, initializationVector, PaddingMode.None);
    }

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
