using System.Text;

namespace Curl.Kerberos;

/// <summary>
/// Adversarial black-box tests of the library's public surface (BL-1501, by the method in
/// Documentation/Wiki/Adversarial-Testing.md): DER lengths in the long, indefinite and
/// overflowing forms, unexpected tags, truncated and mutated messages, credential caches,
/// keytabs and GSS tokens, unsupported encryption types, ciphertext lengths at every
/// boundary, and an encryption type shared across threads. Every refusal must be the typed
/// exception the member's doc comment promises, never an ASN.1, index or argument exception.
/// Mutations come from a seeded <see cref="Random" />; a failure lists the seed, the
/// iteration and the mutated bytes, so it reproduces.
/// </summary>
[TestClass]
public sealed class KerberosAdversarialTests
{
    private const int Iterations = 400;

    private static readonly byte[] Message = Encoding.ASCII.GetBytes("GSS-API message");

    private static readonly KerberosEncryptionType[] EncryptionTypes =
    [
        KerberosEncryptionType.Des3CbcSha1,
        KerberosEncryptionType.Aes128CtsHmacSha196,
        KerberosEncryptionType.Aes256CtsHmacSha196,
        KerberosEncryptionType.Aes128CtsHmacSha256128,
        KerberosEncryptionType.Aes256CtsHmacSha384192,
        KerberosEncryptionType.Rc4Hmac,
        KerberosEncryptionType.Camellia128CtsCmac,
        KerberosEncryptionType.Camellia256CtsCmac,
    ];

    // Boundaries: DER and BER length forms on the outer [APPLICATION 11] of a recorded AS-REP.

    [TestMethod]
    public void KdcReplyDecode_OuterLengthInLongFormWithLeadingZeros_DecodesAsTheDerForm()
    {
        byte[] longForm = [0x6B, 0x84, 0x00, 0x00, 0x03, 0x1E, .. RecordedKerberosMessages.AsReply[4..]];

        KerberosKdcReply reply = KerberosKdcReply.Decode(longForm);

        CollectionAssert.AreEqual(KerberosKdcReply.Decode(RecordedKerberosMessages.AsReply).Encode(), reply.Encode());
    }

    [TestMethod]
    [DataRow(new byte[] { 0x85, 0x00, 0x00, 0x00, 0x03, 0x1E }, DisplayName = "five length bytes")]
    [DataRow(new byte[] { 0x89, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x1E }, DisplayName = "nine length bytes")]
    public void KdcReplyDecode_OuterLengthPaddedPastFourBytesWithZeros_DecodesAsTheDerForm(byte[] length)
    {
        byte[] padded = [0x6B, .. length, .. RecordedKerberosMessages.AsReply[4..]];

        KerberosKdcReply reply = KerberosKdcReply.Decode(padded);

        CollectionAssert.AreEqual(KerberosKdcReply.Decode(RecordedKerberosMessages.AsReply).Encode(), reply.Encode());
    }

    [TestMethod]
    public void KdcReplyDecode_OuterIndefiniteLength_DecodesAsTheDerForm()
    {
        byte[] indefinite = [0x6B, 0x80, .. RecordedKerberosMessages.AsReply[4..], 0x00, 0x00];

        KerberosKdcReply reply = KerberosKdcReply.Decode(indefinite);

        CollectionAssert.AreEqual(KerberosKdcReply.Decode(RecordedKerberosMessages.AsReply).Encode(), reply.Encode());
    }

    [TestMethod]
    public void KdcReplyDecode_IndefiniteLengthWithoutEndOfContents_FailsAsMalformed()
    {
        byte[] unterminated = [0x6B, 0x80, .. RecordedKerberosMessages.AsReply[4..]];

        AssertMessageFails(KerberosMessageError.Malformed, () => KerberosKdcReply.Decode(unterminated));
    }

    [TestMethod]
    [DataRow(new byte[] { 0x84, 0xFF, 0xFF, 0xFF, 0xFF }, DisplayName = "four length bytes, 2^32 - 1")]
    [DataRow(new byte[] { 0x84, 0x7F, 0xFF, 0xFF, 0xFF }, DisplayName = "four length bytes, int.MaxValue")]
    [DataRow(new byte[] { 0xFF }, DisplayName = "reserved length byte 0xFF")]
    [DataRow(new byte[] { 0x82, 0x03, 0x1F }, DisplayName = "one byte longer than the data")]
    [DataRow(new byte[] { 0x82, 0x03, 0x1D }, DisplayName = "one byte shorter than the data")]
    public void KdcReplyDecode_OuterLengthOverflowingOrWrong_FailsAsMalformed(byte[] length)
    {
        byte[] message = [0x6B, .. length, .. RecordedKerberosMessages.AsReply[4..]];

        AssertMessageFails(KerberosMessageError.Malformed, () => KerberosKdcReply.Decode(message));
    }

    // Invalid partitions: the outer tag in each class other than the expected application tag.

    [TestMethod]
    [DataRow((byte)0x30, DisplayName = "universal SEQUENCE")]
    [DataRow((byte)0xAB, DisplayName = "context-specific [11]")]
    [DataRow((byte)0xEB, DisplayName = "private [11]")]
    [DataRow((byte)0x4B, DisplayName = "primitive application [11]")]
    [DataRow((byte)0x7F, DisplayName = "application in the high-tag-number form, cut off")]
    [DataRow((byte)0x00, DisplayName = "end-of-contents tag")]
    public void KdcReplyDecode_UnexpectedOuterTag_FailsWithAMessageException(byte tag)
    {
        byte[] message = [tag, .. RecordedKerberosMessages.AsReply[1..]];

        Assert.ThrowsExactly<KerberosMessageException>(() => KerberosKdcReply.Decode(message));
    }

    // Malformed input: every decoder against seeded mutations of every recorded message.

    [TestMethod]
    [DataRow(1501)]
    [DataRow(4120)]
    public void EveryMessageDecoder_MutatedRecordedMessages_RefusesOnlyWithAMessageException(int seed)
    {
        Func<ReadOnlyMemory<byte>, object>[] decoders =
        [
            bytes => KerberosKdcReply.Decode(bytes),
            bytes => KerberosKdcRequest.Decode(bytes),
            bytes => KerberosErrorMessage.Decode(bytes),
            bytes => KerberosApRequest.Decode(bytes),
            bytes => KerberosApReply.Decode(bytes),
            bytes => KerberosTicket.Decode(bytes),
            bytes => KerberosAuthenticator.Decode(bytes),
            bytes => KerberosEncryptedApReplyPart.Decode(bytes),
            bytes => KerberosEncryptedKdcReplyPart.Decode(bytes),
            bytes => KerberosEncryptedData.Decode(bytes),
            bytes => KerberosEncryptedTimestamp.Decode(bytes),
            bytes => KerberosCredentialMessage.Decode(bytes),
            bytes => KerberosEncryptedCredentialPart.Decode(bytes),
            bytes => KerberosKdcProxyMessage.Decode(bytes),
        ];
        byte[][] recorded =
        [
            RecordedKerberosMessages.AsRequestWithoutPreAuthentication,
            RecordedKerberosMessages.PreAuthenticationRequiredError,
            RecordedKerberosMessages.AsRequestWithEncryptedTimestamp,
            RecordedKerberosMessages.AsReply,
            RecordedKerberosMessages.TgsRequest,
            RecordedKerberosMessages.TgsReply,
            KerberosKdcReply.Decode(RecordedKerberosMessages.AsReply).Ticket.Encode(),
        ];
        Random random = new(seed);
        List<string> failures = [];

        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            byte[] mutated = Mutate(random, recorded[random.Next(recorded.Length)]);
            foreach (Func<ReadOnlyMemory<byte>, object> decode in decoders)
            {
                RecordUnexpected<KerberosMessageException>(failures, seed, iteration, mutated, () => decode(mutated));
            }
        }

        Assert.AreEqual(string.Empty, string.Join(Environment.NewLine, failures));
    }

    [TestMethod]
    public void KdcReplyDecode_TruncatedAtEveryOffset_FailsAsMalformed()
    {
        byte[] reply = RecordedKerberosMessages.TgsReply;
        List<int> notMalformed = [];

        for (int length = 0; length < reply.Length; length++)
        {
            try
            {
                KerberosKdcReply.Decode(reply.AsMemory(0, length));
                notMalformed.Add(length);
            }
            catch (KerberosMessageException exception) when (exception.Error == KerberosMessageError.Malformed)
            {
            }
        }

        Assert.AreEqual(string.Empty, string.Join(", ", notMalformed), "lengths that did not fail as Malformed");
    }

    // Boundaries and malformed input: credential cache files.

    [TestMethod]
    public void CredentialCacheRead_TruncatedAtEveryOffset_FailsAsTruncatedOrEndsAtACredential()
    {
        byte[] cache = RecordedKerberosFiles.AliceCredentialCache;
        List<string> failures = [];

        for (int length = 0; length < cache.Length; length++)
        {
            byte[] truncated = cache[..length];
            RecordUnexpected<KerberosFileException>(failures, 0, length, truncated, () => CredentialCacheReader.Read(truncated));
        }

        Assert.AreEqual(string.Empty, string.Join(Environment.NewLine, failures));
        Assert.AreEqual(KerberosFileError.Truncated, Assert.ThrowsExactly<KerberosFileException>(() => CredentialCacheReader.Read(cache[..^1])).Error);
    }

    [TestMethod]
    public void CredentialCacheRead_ComponentCountOfUInt32Max_FailsAsTruncated()
    {
        byte[] cache = new BigEndianBytes().Byte(0x05).Byte(0x04).UInt16(0)
            .Int32(1).UInt32(uint.MaxValue).String32("EXAMPLE.TEST").String32("alice").ToArray();

        Assert.AreEqual(KerberosFileError.Truncated, Assert.ThrowsExactly<KerberosFileException>(() => CredentialCacheReader.Read(cache)).Error);
    }

    [TestMethod]
    [DataRow(ushort.MaxValue, DisplayName = "header length 65535 past the end")]
    [DataRow((ushort)1, DisplayName = "header length 1, cutting a tag in half")]
    public void CredentialCacheRead_HeaderLengthWrong_FailsAsTruncated(ushort headerLength)
    {
        byte[] cache = new BigEndianBytes().Byte(0x05).Byte(0x04).UInt16(headerLength).UInt16(1).ToArray();

        Assert.AreEqual(KerberosFileError.Truncated, Assert.ThrowsExactly<KerberosFileException>(() => CredentialCacheReader.Read(cache)).Error);
    }

    [TestMethod]
    public void CredentialCacheRead_DeltaTimeShorterThanEightBytes_FailsAsTruncated()
    {
        byte[] cache = new BigEndianBytes().Byte(0x05).Byte(0x04).UInt16(11)
            .UInt16(1).UInt16(7).Int32(1).Raw(0, 0, 0).ToArray();

        Assert.AreEqual(KerberosFileError.Truncated, Assert.ThrowsExactly<KerberosFileException>(() => CredentialCacheReader.Read(cache)).Error);
    }

    [TestMethod]
    public void CredentialCacheRead_DeltaTimeAtInt32Extremes_ReadsTheOffset()
    {
        byte[] cache = new BigEndianBytes().Byte(0x05).Byte(0x04).UInt16(12)
            .UInt16(1).UInt16(8).Int32(int.MinValue).Int32(int.MinValue)
            .Int32(1).UInt32(1).String32("EXAMPLE.TEST").String32("alice").ToArray();

        CredentialCache read = CredentialCacheReader.Read(cache);

        Assert.AreEqual(TimeSpan.FromSeconds(int.MinValue) + TimeSpan.FromMicroseconds(int.MinValue), read.KdcTimeOffset);
        Assert.IsEmpty(read.Credentials);
    }

    [TestMethod]
    [DataRow(1501)]
    [DataRow(3961)]
    public void CredentialCacheRead_MutatedRecordedCache_RefusesOnlyWithAFileException(int seed)
    {
        Random random = new(seed);
        List<string> failures = [];

        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            byte[] mutated = Mutate(random, RecordedKerberosFiles.AliceCredentialCache);
            RecordUnexpected<KerberosFileException>(failures, seed, iteration, mutated, () => CredentialCacheReader.Read(mutated));
        }

        Assert.AreEqual(string.Empty, string.Join(Environment.NewLine, failures));
    }

    // Boundaries and malformed input: keytab files.

    [TestMethod]
    [DataRow(int.MinValue, DisplayName = "hole of 2^31 bytes")]
    [DataRow(int.MaxValue, DisplayName = "entry of int.MaxValue bytes")]
    [DataRow(5, DisplayName = "entry one byte longer than the four left")]
    public void KeytabRead_EntrySizeRunningPastTheEnd_FailsAsTruncated(int size)
    {
        byte[] keytab = new BigEndianBytes().Byte(0x05).Byte(0x02).Int32(size).Raw(0, 0, 0, 0).ToArray();

        Assert.AreEqual(KerberosFileError.Truncated, Assert.ThrowsExactly<KerberosFileException>(() => KeytabReader.Read(keytab)).Error);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "no bytes after the version")]
    [DataRow(1, DisplayName = "one byte after the version")]
    [DataRow(3, DisplayName = "three bytes after the version, one short of a size")]
    public void KeytabRead_FewerThanFourBytesAfterTheVersion_IsEmpty(int trailing)
    {
        byte[] keytab = [0x05, 0x02, .. new byte[trailing]];

        Assert.IsEmpty(KeytabReader.Read(keytab).Entries);
    }

    [TestMethod]
    public void KeytabRead_HoleOfExactlyTheRemainingBytes_IsEmpty()
    {
        byte[] keytab = new BigEndianBytes().Byte(0x05).Byte(0x02).Int32(-4).Raw(1, 2, 3, 4).ToArray();

        Assert.IsEmpty(KeytabReader.Read(keytab).Entries);
    }

    [TestMethod]
    [DataRow(1, DisplayName = "entry of one byte")]
    [DataRow(3, DisplayName = "entry of three bytes")]
    public void KeytabRead_EntryTooShortForItsFields_FailsAsTruncated(int size)
    {
        byte[] keytab = new BigEndianBytes().Byte(0x05).Byte(0x02).Int32(size).Raw(new byte[size]).ToArray();

        Assert.AreEqual(KerberosFileError.Truncated, Assert.ThrowsExactly<KerberosFileException>(() => KeytabReader.Read(keytab)).Error);
    }

    [TestMethod]
    [DataRow(new byte[] { }, DisplayName = "empty")]
    [DataRow(new byte[] { 0x05 }, DisplayName = "marker only")]
    public void KeytabRead_ShorterThanTheVersion_FailsAsTruncated(byte[] keytab)
    {
        Assert.AreEqual(KerberosFileError.Truncated, Assert.ThrowsExactly<KerberosFileException>(() => KeytabReader.Read(keytab)).Error);
    }

    [TestMethod]
    [DataRow((byte)0x05, (byte)0x01, DisplayName = "version 1")]
    [DataRow((byte)0x05, (byte)0x03, DisplayName = "version 3")]
    [DataRow((byte)0x04, (byte)0x02, DisplayName = "wrong marker")]
    public void KeytabRead_VersionOtherThanTwo_FailsAsUnknownVersion(byte marker, byte version)
    {
        byte[] keytab = [marker, version, 0, 0, 0, 0];

        Assert.AreEqual(KerberosFileError.UnknownVersion, Assert.ThrowsExactly<KerberosFileException>(() => KeytabReader.Read(keytab)).Error);
    }

    [TestMethod]
    [DataRow(1501)]
    [DataRow(4757)]
    public void KeytabRead_MutatedRecordedKeytab_RefusesOnlyWithAFileException(int seed)
    {
        Random random = new(seed);
        List<string> failures = [];

        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            byte[] mutated = Mutate(random, RecordedKerberosFiles.HttpServiceKeytab);
            RecordUnexpected<KerberosFileException>(failures, seed, iteration, mutated, () => KeytabReader.Read(mutated));
        }

        Assert.AreEqual(string.Empty, string.Join(Environment.NewLine, failures));
    }

    // Invalid partitions: encryption type numbers the library does not have.

    [TestMethod]
    [DataRow(int.MinValue, DisplayName = "int.MinValue")]
    [DataRow(-128, DisplayName = "negative, Microsoft's rc4-hmac-old")]
    [DataRow(0, DisplayName = "zero")]
    [DataRow(1, DisplayName = "des-cbc-crc")]
    [DataRow(3, DisplayName = "des-cbc-md5")]
    [DataRow(24, DisplayName = "rc4-hmac-exp, between rc4-hmac and camellia")]
    [DataRow(27, DisplayName = "one past camellia256-cts-cmac")]
    [DataRow(int.MaxValue, DisplayName = "int.MaxValue")]
    public void EncryptionCreate_UnsupportedEncryptionType_FailsAsUnsupportedEncryptionType(int encryptionType)
    {
        KerberosCryptographyException failure = Assert.ThrowsExactly<KerberosCryptographyException>(
            () => KerberosEncryption.Create((KerberosEncryptionType)encryptionType, new FixedKerberosRandomSource(new byte[64])));

        Assert.AreEqual(KerberosCryptographyError.UnsupportedEncryptionType, failure.Error);
    }

    [TestMethod]
    public void GssContext_ServiceTicketOfAnUnsupportedEncryptionType_FailsAsUnsupportedEncryptionType()
    {
        KerberosCredential ticket = new FakeGssAcceptor(KerberosEncryptionType.Aes256CtsHmacSha196).ServiceTicket();
        KerberosCredential desTicket = new()
        {
            Client = ticket.Client,
            Server = ticket.Server,
            SessionKey = new KerberosKey(3, new byte[8]),
            Ticket = ticket.Ticket,
            Flags = ticket.Flags,
            AuthenticationTime = ticket.AuthenticationTime,
            EndTime = ticket.EndTime,
        };

        KerberosCryptographyException failure = Assert.ThrowsExactly<KerberosCryptographyException>(
            () => new KerberosGssContext(desTicket, new KerberosGssContextOptions(), new FixedTimeProvider(FakeKdc.Now), new FixedKerberosRandomSource(new byte[64])));

        Assert.AreEqual(KerberosCryptographyError.UnsupportedEncryptionType, failure.Error);
    }

    // Boundaries: key sizes and ciphertext lengths for every encryption type.

    [TestMethod]
    public void EveryEncryptionType_KeyOneByteShortOrLong_ThrowsArgumentException()
    {
        foreach (KerberosEncryptionType encryptionType in EncryptionTypes)
        {
            KerberosEncryption encryption = KerberosEncryption.Create(encryptionType, new FixedKerberosRandomSource(new byte[64]));
            foreach (int keyLength in new[] { 0, encryption.KeySize - 1, encryption.KeySize + 1 })
            {
                byte[] key = new byte[keyLength];
                Assert.ThrowsExactly<ArgumentException>(() => encryption.Encrypt(key, 1, Message), $"{encryptionType} Encrypt, key of {keyLength}");
                Assert.ThrowsExactly<ArgumentException>(() => encryption.Decrypt(key, 1, new byte[64]), $"{encryptionType} Decrypt, key of {keyLength}");
                Assert.ThrowsExactly<ArgumentException>(() => encryption.ComputeChecksum(key, 1, Message), $"{encryptionType} ComputeChecksum, key of {keyLength}");
                Assert.ThrowsExactly<ArgumentException>(() => encryption.ComputePseudoRandom(key, Message), $"{encryptionType} ComputePseudoRandom, key of {keyLength}");
            }
        }
    }

    [TestMethod]
    public void EveryEncryptionType_CiphertextOfEveryLengthUpToThreeBlocksPastTheMinimum_RefusesOnlyWithACryptographyException()
    {
        List<string> failures = [];

        foreach (KerberosEncryptionType encryptionType in EncryptionTypes)
        {
            KerberosEncryption encryption = KerberosEncryption.Create(encryptionType, new FixedKerberosRandomSource(new byte[64]));
            byte[] key = Enumerable.Range(100, encryption.KeySize).Select(value => (byte)value).ToArray();
            int shortest = encryption.Encrypt(key, 1, []).Length;
            for (int length = 0; length <= shortest + 48; length++)
            {
                byte[] ciphertext = new byte[length];
                RecordUnexpected<KerberosCryptographyException>(failures, (int)encryptionType, length, ciphertext, () => encryption.Decrypt(key, 1, ciphertext));
            }

            Assert.AreEqual(
                KerberosCryptographyError.CiphertextTooShort,
                Assert.ThrowsExactly<KerberosCryptographyException>(() => encryption.Decrypt(key, 1, new byte[shortest - 1])).Error,
                $"{encryptionType}: one byte shorter than an empty message's ciphertext");
        }

        Assert.AreEqual(string.Empty, string.Join(Environment.NewLine, failures));
    }

    [TestMethod]
    public void EveryEncryptionType_EveryBitOfAShortCiphertextFlipped_FailsTheIntegrityCheck()
    {
        List<string> passed = [];

        foreach (KerberosEncryptionType encryptionType in EncryptionTypes)
        {
            KerberosEncryption encryption = KerberosEncryption.Create(encryptionType, new FixedKerberosRandomSource(Enumerable.Range(1, 64).Select(value => (byte)value).ToArray()));
            byte[] key = Enumerable.Range(100, encryption.KeySize).Select(value => (byte)value).ToArray();
            byte[] ciphertext = encryption.Encrypt(key, 3, Message);
            for (int bit = 0; bit < ciphertext.Length * 8; bit++)
            {
                byte[] altered = [.. ciphertext];
                altered[bit / 8] ^= (byte)(1 << (bit % 8));
                try
                {
                    encryption.Decrypt(key, 3, altered);
                    passed.Add($"{encryptionType} bit {bit}");
                }
                catch (KerberosCryptographyException exception) when (exception.Error == KerberosCryptographyError.IntegrityCheckFailed)
                {
                }
            }
        }

        Assert.AreEqual(string.Empty, string.Join(", ", passed), "flipped bits that still decrypted");
    }

    [TestMethod]
    [DataRow(new byte[] { 0x00, 0x00, 0x10 }, DisplayName = "three bytes")]
    [DataRow(new byte[] { 0x00, 0x00, 0x10, 0x00, 0x00 }, DisplayName = "five bytes")]
    [DataRow(new byte[] { 0x01, 0x00, 0x00, 0x00 }, DisplayName = "2^24 iterations, one past MIT's limit")]
    [DataRow(new byte[] { 0x00, 0x00, 0x00, 0x00 }, DisplayName = "zero, meaning 2^32")]
    public void Aes256StringToKey_IterationCountParametersOutOfRange_FailsAsBadStringToKeyParameters(byte[] parameters)
    {
        KerberosEncryption encryption = KerberosEncryption.Create(KerberosEncryptionType.Aes256CtsHmacSha196, new FixedKerberosRandomSource(new byte[64]));

        KerberosCryptographyException failure = Assert.ThrowsExactly<KerberosCryptographyException>(
            () => encryption.StringToKey("password", Encoding.UTF8.GetBytes("EXAMPLE.TESTalice"), parameters));

        Assert.AreEqual(KerberosCryptographyError.BadStringToKeyParameters, failure.Error);
    }

    // State and concurrency: one encryption type shared by many threads.

    [TestMethod]
    public async Task EveryEncryptionType_SharedAcrossThreads_RoundTripsEveryMessage()
    {
        foreach (KerberosEncryptionType encryptionType in EncryptionTypes)
        {
            KerberosEncryption encryption = KerberosEncryption.Create(encryptionType, new FixedKerberosRandomSource(Enumerable.Range(1, 64).Select(value => (byte)value).ToArray()));
            byte[] key = Enumerable.Range(7, encryption.KeySize).Select(value => (byte)value).ToArray();

            string[] roundTripped = await Task.WhenAll(Enumerable.Range(0, 64).Select(index => Task.Run(() =>
            {
                byte[] plaintext = Encoding.ASCII.GetBytes($"message {index}");
                // des3-cbc-sha1 gives its zero padding back with the plaintext.
                return Encoding.ASCII.GetString(encryption.Decrypt(key, index, encryption.Encrypt(key, index, plaintext))).TrimEnd('\0');
            })));

            CollectionAssert.AreEqual(Enumerable.Range(0, 64).Select(index => $"message {index}").ToArray(), roundTripped, encryptionType.ToString());
        }
    }

    // State: the GSS context's order of calls and the acceptor's tokens.

    [TestMethod]
    public void GssContext_PerMessageCallsBeforeTheContextIsEstablished_ThrowInvalidOperation()
    {
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        using KerberosGssContext context = KerberosGssContextTests.NewContext(acceptor);
        context.NextToken([]);

        Assert.ThrowsExactly<InvalidOperationException>(() => context.Wrap(Message, encrypt: true));
        Assert.ThrowsExactly<InvalidOperationException>(() => context.Unwrap(Message));
        Assert.ThrowsExactly<InvalidOperationException>(() => context.GetMic(Message));
        Assert.ThrowsExactly<InvalidOperationException>(() => context.VerifyMic(Message, Message));
    }

    [TestMethod]
    public void GssContext_NextTokenAfterTheContextIsEstablished_ThrowsInvalidOperation()
    {
        using KerberosGssContext context = KerberosGssContextTests.Establish(new FakeGssAcceptor(KerberosEncryptionType.Aes256CtsHmacSha196));

        Assert.ThrowsExactly<InvalidOperationException>(() => context.NextToken([]));
    }

    [TestMethod]
    [DataRow(-1, DisplayName = "one microsecond early")]
    [DataRow(1_000_000, DisplayName = "one second late")]
    [DataRow(-500_000, DisplayName = "microseconds echoed as zero")]
    public void GssContext_ApReplyEchoingAnotherAuthenticatorTime_FailsMutualAuthentication(int microsecondsOffset)
    {
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196) { EchoedMicrosecondsOffset = microsecondsOffset };
        using KerberosGssContext context = KerberosGssContextTests.NewContext(acceptor);
        acceptor.Accept(context.NextToken([]));

        KerberosGssException failure = Assert.ThrowsExactly<KerberosGssException>(() => context.NextToken(acceptor.Reply()));

        Assert.AreEqual(KerberosGssError.MutualAuthenticationFailed, failure.Error);
    }

    [TestMethod]
    public void GssContext_ApReplyTruncatedAtEveryOffset_RefusesOnlyWithAGssException()
    {
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        KerberosGssContext probe = KerberosGssContextTests.NewContext(acceptor);
        acceptor.Accept(probe.NextToken([]));
        byte[] reply = acceptor.Reply();
        probe.Dispose();
        List<string> failures = [];

        for (int length = 0; length < reply.Length; length++)
        {
            using KerberosGssContext context = KerberosGssContextTests.NewContext(acceptor);
            context.NextToken([]);
            byte[] truncated = reply[..length];
            RecordUnexpected<KerberosGssException>(failures, 0, length, truncated, () => context.NextToken(truncated));
            Assert.IsFalse(context.IsCompleted, $"established by an AP-REP cut to {length} bytes");
        }

        Assert.AreEqual(string.Empty, string.Join(Environment.NewLine, failures));
    }

    [TestMethod]
    [DataRow(KerberosEncryptionType.Aes256CtsHmacSha196, 1501)]
    [DataRow(KerberosEncryptionType.Aes128CtsHmacSha256128, 4121)]
    [DataRow(KerberosEncryptionType.Rc4Hmac, 4757)]
    public void GssContext_MutatedAcceptorTokens_RefuseOnlyWithAGssException(KerberosEncryptionType encryptionType, int seed)
    {
        FakeGssAcceptor acceptor = new(encryptionType);
        using KerberosGssContext context = KerberosGssContextTests.Establish(acceptor);
        uint sequence = acceptor.AcceptorSequence!.Value;
        bool rc4 = encryptionType == KerberosEncryptionType.Rc4Hmac;
        byte[][] tokens = rc4
            ? [acceptor.Rc4Wrap(Message, sequence, encrypt: true), acceptor.Rc4Wrap(Message, sequence, encrypt: false), acceptor.Rc4Mic(Message, sequence)]
            : [acceptor.Rfc4121Wrap(Message, sequence, encrypt: true), acceptor.Rfc4121Wrap(Message, sequence, encrypt: false), acceptor.Rfc4121Mic(Message, sequence)];
        Random random = new(seed);
        List<string> failures = [];
        List<string> accepted = [];

        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            int which = random.Next(tokens.Length);
            byte[] mutated = Mutate(random, tokens[which]);
            Action check = which == 2 ? () => context.VerifyMic(Message, mutated) : () => context.Unwrap(mutated);
            if (RecordUnexpected<KerberosGssException>(failures, seed, iteration, mutated, check))
            {
                accepted.Add($"iteration {iteration}: {Convert.ToHexString(mutated)}");
            }
        }

        Assert.AreEqual(string.Empty, string.Join(Environment.NewLine, failures));
        Assert.AreEqual(string.Empty, string.Join(Environment.NewLine, accepted), $"seed {seed}: mutated tokens that were accepted");
    }

    /// <summary>
    /// Changes <paramref name="original" /> one way: a bit flipped, a byte replaced, a cut at
    /// a random length, a byte inserted or removed, or a run of bytes overwritten with 0xFF;
    /// never the bytes it was given.
    /// </summary>
    private static byte[] Mutate(Random random, byte[] original)
    {
        byte[] mutated;
        do
        {
            mutated = MutateOnce(random, original);
        }
        while (mutated.AsSpan().SequenceEqual(original));

        return mutated;
    }

    private static byte[] MutateOnce(Random random, byte[] original)
    {
        byte[] bytes = [.. original];
        int offset = random.Next(bytes.Length);
        switch (random.Next(6))
        {
            case 0:
                bytes[offset] ^= (byte)(1 << random.Next(8));
                return bytes;
            case 1:
                bytes[offset] = (byte)(bytes[offset] + 1 + random.Next(255));
                return bytes;
            case 2:
                return bytes[..offset];
            case 3:
                return [.. bytes[..offset], (byte)random.Next(256), .. bytes[offset..]];
            case 4:
                return [.. bytes[..offset], .. bytes[(offset + 1)..]];
            default:
                bytes.AsSpan(offset, Math.Min(4, bytes.Length - offset)).Fill(0xFF);
                return bytes;
        }
    }

    /// <summary>
    /// Runs <paramref name="call" />, recording any exception other than
    /// <typeparamref name="TExpected" />; gives whether the call returned without one.
    /// </summary>
    private static bool RecordUnexpected<TExpected>(List<string> failures, int seed, int iteration, byte[] input, Action call)
        where TExpected : Exception
    {
        try
        {
            call();
            return true;
        }
        catch (TExpected)
        {
            return false;
        }
        catch (Exception exception)
        {
            failures.Add($"seed {seed}, iteration {iteration}, input {Convert.ToHexString(input)}: {exception.GetType().Name}: {exception.Message}");
            return false;
        }
    }

    private static void AssertMessageFails(KerberosMessageError expected, Action decode) =>
        Assert.AreEqual(expected, Assert.ThrowsExactly<KerberosMessageException>(decode).Error);
}
