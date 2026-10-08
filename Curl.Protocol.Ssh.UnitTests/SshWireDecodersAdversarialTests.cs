using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Curl.Protocol.Ssh.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Attacks the library's public readers of server bytes, <see cref="SshWireDecoders" />,
/// at their boundaries, with malformed and invalid input and under repeated and concurrent
/// calls (BL-1518, <c>Documentation/Wiki/Adversarial-Testing.md</c>). The oracle is RFC
/// 4253 and the decoders' own contract: a refusal is a <see langword="false" /> or a short
/// count, never an exception.
/// </summary>
[TestClass]
public sealed class SshWireDecodersAdversarialTests
{
    // libssh2's LIBSSH2_PACKET_MAXCOMP: a packet, length field and MAC included, is at most
    // 40000 bytes; with no MAC before the first NEWKEYS that is a packet_length of 39996.
    private const uint LargestPacketLength = 39996;

    private const int LargestPayload = 40000;

    private static readonly string[] HostKeyAlgorithms =
    [
        "ssh-ed25519",
        "rsa-sha2-256",
        "ecdsa-sha2-nistp256",
        "ecdsa-sha2-nistp521",
        "ssh-dss",
        "ssh-ed25519-cert-v01@openssh.com",
        "sk-ssh-ed25519@openssh.com",
        "sk-ecdsa-sha2-nistp256@openssh.com",
    ];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    // ---- Boundaries: packet_length ----

    [TestMethod]
    public async Task CountWholePacketsAsync_PacketAtTheLargestLength_IsRead()
    {
        byte[] bytes = PacketOfLength(LargestPacketLength, paddingLength: 4);
        ArrangeBytesSummary($"wire: one packet, packet_length {LargestPacketLength}, padding 4", bytes);

        int count = await SshWireDecoders.CountWholePacketsAsync(bytes, CancellationToken.None);

        AssertCount(1, count);
    }

    [TestMethod]
    public async Task CountWholePacketsAsync_PacketOneBlockPastTheLargestLength_IsRefused()
    {
        byte[] bytes = [.. new SshServerScript().Packet(2).Bytes, .. PacketOfLength(LargestPacketLength + 8, paddingLength: 4)];
        ArrangeBytesSummary($"wire: message 2, then packet_length {LargestPacketLength + 8}", bytes);

        int count = await SshWireDecoders.CountWholePacketsAsync(bytes, CancellationToken.None);

        AssertCount(1, count);
    }

    [TestMethod]
    public async Task CountWholePacketsAsync_PacketAt35000Bytes_TheRfcMinimumEveryImplementationTakes_IsRead()
    {
        // RFC 4253 section 6.1: every implementation must take a 35000-byte packet; 35004
        // is the first block-aligned packet_length at or past it.
        byte[] bytes = PacketOfLength(35004, paddingLength: 4);
        ArrangeBytesSummary("wire: one packet, packet_length 35004", bytes);

        int count = await SshWireDecoders.CountWholePacketsAsync(bytes, CancellationToken.None);

        AssertCount(1, count);
    }

    [TestMethod]
    [DataRow(0xFFFFFFFFu, DisplayName = "uint32 maximum")]
    [DataRow(0x80000000u, DisplayName = "int32 sign bit")]
    [DataRow(0x7FFFFFF8u, DisplayName = "block-aligned near int32 maximum")]
    public async Task CountWholePacketsAsync_PacketLengthFarPastTheMaximum_IsRefusedBeforeReadingTheBody(uint packetLength)
    {
        byte[] bytes = [.. new SshServerScript().Packet(2).Bytes, .. SshTestEncoding.UInt32(packetLength), 4, 2, 0, 0, 0];
        ArrangeBytes($"wire: message 2, then packet_length 0x{packetLength:X8} and five bytes", bytes);

        int count = await SshWireDecoders.CountWholePacketsAsync(bytes, CancellationToken.None);

        AssertCount(1, count);
    }

    [TestMethod]
    public async Task CountWholePacketsAsync_SmallestWholePacket_IsRead()
    {
        // packet_length 12: one padding_length byte, a one-byte payload and the RFC's
        // minimum four padding bytes, rounded up to the eight-byte block with the length field.
        byte[] bytes = new SshServerScript().RawPacket(12, 10, [2, .. new byte[10]]).Bytes;
        ArrangeBytes("wire: packet_length 12, padding 10, payload 02", bytes);

        int count = await SshWireDecoders.CountWholePacketsAsync(bytes, CancellationToken.None);

        AssertCount(1, count);
    }

    [TestMethod]
    public async Task CountWholePacketsAsync_BlockAlignedPacketTooShortForAPayload_IsRefused()
    {
        byte[] bytes = new SshServerScript().RawPacket(4, 3, [2, 0, 0]).Bytes;
        ArrangeBytes("wire: packet_length 4, padding 3", bytes);

        int count = await SshWireDecoders.CountWholePacketsAsync(bytes, CancellationToken.None);

        AssertCount(0, count);
    }

    // ---- Boundaries and invalid partitions: padding_length ----

    [TestMethod]
    [DataRow((byte)4, 1, DisplayName = "four padding bytes, the minimum")]
    [DataRow((byte)3, 0, DisplayName = "three padding bytes, one below the minimum")]
    [DataRow((byte)0, 0, DisplayName = "no padding")]
    [DataRow((byte)18, 1, DisplayName = "padding leaves a one-byte payload")]
    [DataRow((byte)19, 0, DisplayName = "padding leaves no payload")]
    [DataRow((byte)20, 0, DisplayName = "padding runs one byte past the packet")]
    [DataRow((byte)255, 0, DisplayName = "padding of 255 in a 20-byte packet")]
    public async Task CountWholePacketsAsync_PaddingLengthAtEachEdgeOfTheValidRange_IsReadOrRefusedByRfc4253(byte paddingLength, int expected)
    {
        byte[] bytes = new SshServerScript().RawPacket(20, paddingLength, [2, .. new byte[18]]).Bytes;
        ArrangeBytes($"wire: packet_length 20 (24 bytes with its length), padding_length {paddingLength}", bytes);

        int count = await SshWireDecoders.CountWholePacketsAsync(bytes, CancellationToken.None);

        AssertCount(expected, count);
    }

    [TestMethod]
    [DataRow(5u, DisplayName = "one past a block")]
    [DataRow(11u, DisplayName = "one short of a block")]
    [DataRow(13u, DisplayName = "one past the smallest whole packet")]
    [DataRow(39995u, DisplayName = "one short of the largest")]
    public async Task CountWholePacketsAsync_PacketLengthNotAMultipleOfTheBlockSize_IsRefused(uint packetLength)
    {
        byte[] bytes = [.. new SshServerScript().Packet(2).Bytes, .. PacketOfLength(packetLength, paddingLength: 4)];
        ArrangeBytesSummary($"wire: message 2, then packet_length {packetLength}", bytes);

        int count = await SshWireDecoders.CountWholePacketsAsync(bytes, CancellationToken.None);

        AssertCount(1, count);
    }

    // ---- Malformed input: truncation at every offset ----

    [TestMethod]
    public async Task CountWholePacketsAsync_TwoPacketsCutAtEveryOffset_CountsOnlyTheWholeOnes()
    {
        byte[] first = new SshServerScript().Packet(2, 1, 2, 3).Bytes;
        byte[] both = new SshServerScript().Packet(2, 1, 2, 3).Packet([94, .. Enumerable.Repeat((byte)0x5A, 30)]).Bytes;
        Diagnostics.Arrange("wire", $"messages 2 ({first.Length} bytes) and 94 ({both.Length - first.Length} bytes), cut at every offset 0..{both.Length}");

        for (int offset = 0; offset <= both.Length; offset++)
        {
            int expected = offset == both.Length ? 2 : offset >= first.Length ? 1 : 0;

            int count = await SshWireDecoders.CountWholePacketsAsync(both.AsMemory(0, offset), CancellationToken.None);

            if (count != expected)
            {
                Diagnostics.Assert($"whole packets cut at {offset}", expected, count);
            }

            Assert.AreEqual(expected, count, $"cut at offset {offset}");
        }

        Diagnostics.Assert("every cut", "counted only whole packets", "counted only whole packets");
    }

    [TestMethod]
    public async Task CountWholePacketsAsync_SeededRandomBytes_NeverThrow()
    {
        const int Seed = 1518;
        Random random = new(Seed);
        Diagnostics.Arrange("seed", Seed);

        for (int round = 0; round < 500; round++)
        {
            byte[] bytes = new byte[random.Next(0, 64)];
            random.NextBytes(bytes);

            // Keep the length field small enough for the bytes to matter after it.
            if (bytes.Length >= 4 && random.Next(2) == 0)
            {
                BinaryPrimitives.WriteUInt32BigEndian(bytes, (uint)random.Next(0, 64));
            }

            int count = await CountOrReport(bytes, Seed, round);

            Assert.IsTrue(count is >= 0 and <= 4, $"seed {Seed}, round {round}: {count} packets from {bytes.Length} bytes");
        }

        Diagnostics.Assert("500 random inputs", "no exception", "no exception");
    }

    // ---- Malformed input: KEXINIT ----

    [TestMethod]
    public void TryDecodeKexInit_TenEmptyNameLists_ReturnsTrue()
    {
        byte[] payload = KexInit(Enumerable.Repeat(string.Empty, 10).ToArray());
        ArrangeBytes("KEXINIT with ten empty name-lists", payload);

        AssertDecoded(true, SshWireDecoders.TryDecodeKexInit(payload));
    }

    [TestMethod]
    [DataRow(",", DisplayName = "a lone comma")]
    [DataRow(",,,", DisplayName = "three commas")]
    [DataRow("curve25519-sha256,", DisplayName = "a trailing comma")]
    [DataRow(",curve25519-sha256", DisplayName = "a leading comma")]
    [DataRow("a,,b", DisplayName = "an empty name between two")]
    [DataRow(" curve25519-sha256 ", DisplayName = "spaces around a name")]
    public void TryDecodeKexInit_NameListWithStrayCommas_ReadsAsLibssh2Does(string nameList)
    {
        // libssh2 1.11.1 reads a name-list as a comma-separated string and does not refuse
        // empty names; what fails to agree is decided later, by negotiation.
        byte[] payload = KexInit([nameList, .. Enumerable.Repeat("x", 9)]);
        ArrangeBytes($"KEXINIT whose key-exchange list is \"{nameList}\"", payload);

        AssertDecoded(true, SshWireDecoders.TryDecodeKexInit(payload));
    }

    [TestMethod]
    public void TryDecodeKexInit_NameListOfTenThousandNames_ReturnsTrue()
    {
        string huge = string.Join(',', Enumerable.Range(0, 10_000).Select(index => $"kex-{index}@example.com"));
        byte[] payload = KexInit([huge, .. Enumerable.Repeat("x", 9)]);
        ArrangeBytesSummary($"KEXINIT whose key-exchange list holds 10000 names ({huge.Length} bytes)", payload);

        AssertDecoded(true, SshWireDecoders.TryDecodeKexInit(payload));
    }

    [TestMethod]
    public void TryDecodeKexInit_NameListOfNonAsciiBytes_ReturnsTrue()
    {
        byte[][] fields =
        [
            [20],
            new byte[16],
            SshTestEncoding.String([0xFF, 0x00, 0x80, 0x2C, 0xC3, 0xA9]),
            .. Enumerable.Repeat(SshTestEncoding.String([]), 9),
            [0],
            SshTestEncoding.UInt32(0),
        ];
        byte[] payload = SshTestEncoding.Join(fields);
        ArrangeBytes("KEXINIT whose key-exchange list holds FF 00 80 , C3 A9", payload);

        AssertDecoded(true, SshWireDecoders.TryDecodeKexInit(payload));
    }

    [TestMethod]
    [DataRow(0xFFFFFFFFu, DisplayName = "uint32 maximum")]
    [DataRow(0x80000000u, DisplayName = "int32 sign bit")]
    [DataRow(1u, DisplayName = "one byte past the end")]
    public void TryDecodeKexInit_NameListLengthPastThePayload_ReturnsFalse(uint length)
    {
        byte[] payload = SshTestEncoding.Join([20], new byte[16], SshTestEncoding.UInt32(length));
        ArrangeBytes($"KEXINIT cookie, then a name-list length 0x{length:X8} and nothing after it", payload);

        AssertDecoded(false, SshWireDecoders.TryDecodeKexInit(payload));
    }

    [TestMethod]
    public void TryDecodeKexInit_EmptyPayload_ReturnsFalse()
    {
        ArrangeBytes("empty payload", []);

        AssertDecoded(false, SshWireDecoders.TryDecodeKexInit(ReadOnlyMemory<byte>.Empty));
    }

    [TestMethod]
    public void TryDecodeKexInit_KexInitCutAtEveryOffset_ReturnsFalseUntilTheReservedFieldIsWhole()
    {
        byte[] payload = KexInit(["curve25519-sha256", "ssh-ed25519", "aes128-ctr", "aes128-ctr", "hmac-sha2-256", "hmac-sha2-256", "none", "none", "", ""]);
        Diagnostics.Arrange("KEXINIT", $"{payload.Length} bytes, cut at every offset");

        for (int offset = 0; offset < payload.Length; offset++)
        {
            Assert.IsFalse(SshWireDecoders.TryDecodeKexInit(payload.AsMemory(0, offset)), $"cut at offset {offset}");
        }

        AssertDecoded(true, SshWireDecoders.TryDecodeKexInit(payload));
    }

    [TestMethod]
    public void TryDecodeKexInit_BytesAfterTheReservedField_AreIgnoredAsLibssh2Does()
    {
        byte[] payload = [.. KexInit(Enumerable.Repeat("x", 10).ToArray()), 0xDE, 0xAD];
        ArrangeBytes("KEXINIT followed by DE AD", payload);

        AssertDecoded(true, SshWireDecoders.TryDecodeKexInit(payload));
    }

    // ---- Boundaries, malformed input and invalid partitions: SFTP attributes ----

    [TestMethod]
    [DataRow(0x00000000u, 0, DisplayName = "no flags")]
    [DataRow(0x00000001u, 8, DisplayName = "size")]
    [DataRow(0x00000002u, 8, DisplayName = "owner and group")]
    [DataRow(0x00000004u, 4, DisplayName = "permissions")]
    [DataRow(0x00000008u, 8, DisplayName = "access and modify times")]
    [DataRow(0x0000000Fu, 28, DisplayName = "every field")]
    public void TryDecodeSftpAttributes_EachFieldExactlyWholeAndOneByteShort_ReadsOnlyWhenWhole(uint flags, int fieldsLength)
    {
        byte[] attributes = [.. SshTestEncoding.UInt32(flags), .. Enumerable.Repeat((byte)0xFF, fieldsLength)];
        ArrangeBytes($"SFTP attributes, flags 0x{flags:X8}", attributes);

        Assert.IsTrue(SshWireDecoders.TryDecodeSftpAttributes(attributes), "whole");
        if (attributes.Length > 4)
        {
            Assert.IsFalse(SshWireDecoders.TryDecodeSftpAttributes(attributes.AsMemory(0, attributes.Length - 1)), "one byte short");
        }

        AssertDecoded(true, true);
    }

    [TestMethod]
    [DataRow(0x00000000u, 0, DisplayName = "no flags")]
    [DataRow(0x00000001u, 3, DisplayName = "flags cut to three bytes")]
    public void TryDecodeSftpAttributes_FlagsCutShort_ReturnsFalse(uint flags, int length)
    {
        byte[] attributes = SshTestEncoding.UInt32(flags)[..length];
        ArrangeBytes($"SFTP attributes cut to {length} bytes", attributes);

        AssertDecoded(false, SshWireDecoders.TryDecodeSftpAttributes(attributes));
    }

    [TestMethod]
    public void TryDecodeSftpAttributes_UnknownFlagBits_AreIgnored()
    {
        byte[] attributes = SshTestEncoding.UInt32(0x7FFFFFF0);
        ArrangeBytes("SFTP attributes, every flag bit libssh2 does not know (0x7FFFFFF0)", attributes);

        AssertDecoded(true, SshWireDecoders.TryDecodeSftpAttributes(attributes));
    }

    [TestMethod]
    [DataRow(0xFFFFFFFFu, DisplayName = "uint32 maximum")]
    [DataRow(0x80000000u, DisplayName = "int32 sign bit")]
    [DataRow(2u, DisplayName = "two, with one pair present")]
    public void TryDecodeSftpAttributes_ExtendedCountPastTheData_ReturnsFalseWithoutLooping(uint count)
    {
        byte[] attributes = SshTestEncoding.Join(SshTestEncoding.UInt32(0x80000000), SshTestEncoding.UInt32(count), SshTestEncoding.Name("a"), SshTestEncoding.Name("b"));
        ArrangeBytes($"SFTP attributes, flags EXTENDED, count 0x{count:X8}, one pair", attributes);

        AssertDecoded(false, SshWireDecoders.TryDecodeSftpAttributes(attributes));
    }

    [TestMethod]
    public void TryDecodeSftpAttributes_ExtendedStringLengthPastTheData_ReturnsFalse()
    {
        byte[] attributes = SshTestEncoding.Join(SshTestEncoding.UInt32(0x80000000), SshTestEncoding.UInt32(1), SshTestEncoding.UInt32(0xFFFFFFFF));
        ArrangeBytes("SFTP attributes, flags EXTENDED, count 1, name length 0xFFFFFFFF", attributes);

        AssertDecoded(false, SshWireDecoders.TryDecodeSftpAttributes(attributes));
    }

    [TestMethod]
    public void TryDecodeSftpAttributes_SizeOfUInt64Maximum_ReturnsTrue()
    {
        byte[] attributes = [.. SshTestEncoding.UInt32(1), .. Enumerable.Repeat((byte)0xFF, 8)];
        ArrangeBytes("SFTP attributes, flags SIZE, size 2^64 - 1", attributes);

        AssertDecoded(true, SshWireDecoders.TryDecodeSftpAttributes(attributes));
    }

    // ---- Malformed input and invalid partitions: host keys and signatures ----

    [TestMethod]
    [DynamicData(nameof(HostKeyAlgorithmRows))]
    public void TryDecodeHostKeySignature_EmptyHostKeyAndSignature_ReturnsFalse(string algorithm)
    {
        byte[] bytes = SshTestEncoding.Join(SshTestEncoding.Name(algorithm), SshTestEncoding.String([]), SshTestEncoding.String([]), new byte[32]);
        ArrangeBytes($"{algorithm}, empty host key, empty signature", bytes);

        AssertDecoded(false, SshWireDecoders.TryDecodeHostKeySignature(bytes));
    }

    [TestMethod]
    [DynamicData(nameof(HostKeyAlgorithmRows))]
    public void TryDecodeHostKeySignature_SignatureWithOneBitFlippedInItsLastByte_StillReadsWhole(string algorithm)
    {
        TestHostKey key = TestHostKey.For(algorithm);
        byte[] exchangeHash = [.. Enumerable.Range(0, 32).Select(value => (byte)value)];
        byte[] signature = key.Sign(exchangeHash);
        signature[^1] ^= 0x01;
        byte[] bytes = SshTestEncoding.Join(SshTestEncoding.Name(algorithm), SshTestEncoding.String(key.Blob), SshTestEncoding.String(signature), exchangeHash);
        ArrangeBytesSummary($"{algorithm} host key and a signature with its last bit flipped", bytes);

        AssertDecoded(true, SshWireDecoders.TryDecodeHostKeySignature(bytes));
    }

    [TestMethod]
    [DynamicData(nameof(HostKeyAlgorithmRows))]
    public void TryDecodeHostKeySignature_HostKeyCutAtEveryOffsetBeforeItsKeyEnds_ReturnsFalse(string algorithm)
    {
        TestHostKey key = TestHostKey.For(algorithm);
        byte[] exchangeHash = new byte[32];
        byte[] signature = key.Sign(exchangeHash);
        int keyEnd = KeyEnd(algorithm, key.Blob);
        Diagnostics.Arrange("host key", $"{algorithm}, {key.Blob.Length} bytes, its key ending at {keyEnd}, cut at every offset before it");

        for (int offset = 0; offset < keyEnd; offset++)
        {
            byte[] bytes = SshTestEncoding.Join(SshTestEncoding.Name(algorithm), SshTestEncoding.String(key.Blob[..offset]), SshTestEncoding.String(signature), exchangeHash);

            Assert.IsFalse(DecodeOrReport(bytes, $"host key cut at {offset}"), $"host key cut at offset {offset}");
        }

        AssertDecoded(false, false);
    }

    [TestMethod]
    [DynamicData(nameof(HostKeyAlgorithmRows))]
    public void TryDecodeHostKeySignature_SignatureCutAtEveryOffset_NeverThrows(string algorithm)
    {
        TestHostKey key = TestHostKey.For(algorithm);
        byte[] exchangeHash = new byte[32];
        byte[] signature = key.Sign(exchangeHash);
        Diagnostics.Arrange("signature", $"{algorithm}, {signature.Length} bytes, cut at every offset");

        for (int offset = 0; offset < signature.Length; offset++)
        {
            byte[] bytes = SshTestEncoding.Join(SshTestEncoding.Name(algorithm), SshTestEncoding.String(key.Blob), SshTestEncoding.String(signature[..offset]), exchangeHash);

            DecodeOrReport(bytes, $"signature cut at {offset}");
        }

        AssertDecoded("no exception", "no exception");
    }

    [TestMethod]
    [DynamicData(nameof(HostKeyAlgorithmRows))]
    public void TryDecodeHostKeySignature_SeededRandomByteChangesToKeyAndSignature_NeverThrow(string algorithm)
    {
        const int Seed = 4253;
        Random random = new(Seed);
        TestHostKey key = TestHostKey.For(algorithm);
        byte[] exchangeHash = new byte[32];
        byte[] signature = key.Sign(exchangeHash);
        Diagnostics.Arrange("seed", Seed);

        for (int round = 0; round < 300; round++)
        {
            byte[] blob = Mutate(key.Blob, random);
            byte[] mutatedSignature = Mutate(signature, random);
            byte[] bytes = SshTestEncoding.Join(SshTestEncoding.Name(algorithm), SshTestEncoding.String(blob), SshTestEncoding.String(mutatedSignature), exchangeHash);

            DecodeOrReport(bytes, $"seed {Seed}, round {round}");
        }

        AssertDecoded("no exception", "no exception");
    }

    [TestMethod]
    public void TryDecodeHostKeySignature_RsaKeyWithANegativeExponentMpint_ReturnsFalse()
    {
        byte[] blob = SshTestEncoding.Join(SshTestEncoding.Name("ssh-rsa"), SshTestEncoding.String([0x81, 0x00, 0x01]), SshTestEncoding.String([0x00, .. Enumerable.Repeat((byte)0xC3, 256)]));
        byte[] bytes = SshTestEncoding.Join(SshTestEncoding.Name("rsa-sha2-256"), SshTestEncoding.String(blob), SshTestEncoding.String([]), new byte[32]);
        ArrangeBytesSummary("rsa-sha2-256 host key whose exponent mpint is 81 00 01 (negative)", bytes);

        AssertDecoded(false, SshWireDecoders.TryDecodeHostKeySignature(bytes));
    }

    [TestMethod]
    public void TryDecodeHostKeySignature_RsaKeyWithExtraLeadingZerosInItsMpints_StillReadsWhole()
    {
        TestHostKey key = TestHostKey.For("rsa-sha2-256");
        byte[] exchangeHash = new byte[32];
        byte[] signature = key.Sign(exchangeHash);
        byte[] padded = PadRsaMpints(key.Blob);
        byte[] bytes = SshTestEncoding.Join(SshTestEncoding.Name("rsa-sha2-256"), SshTestEncoding.String(padded), SshTestEncoding.String(signature), exchangeHash);
        ArrangeBytesSummary("rsa-sha2-256 host key whose exponent and modulus each carry three extra leading zero bytes", bytes);

        AssertDecoded(true, SshWireDecoders.TryDecodeHostKeySignature(bytes));
    }

    [TestMethod]
    [DataRow(new byte[] { 0x04 }, DisplayName = "only the uncompressed-point tag")]
    [DataRow(new byte[] { 0x02, 0x01 }, DisplayName = "compressed-point tag")]
    [DataRow(new byte[] { 0x00 }, DisplayName = "point at infinity")]
    public void TryDecodeHostKeySignature_EcdsaKeyWithAMalformedPoint_ReturnsFalse(byte[] point)
    {
        byte[] blob = SshTestEncoding.Join(SshTestEncoding.Name("ecdsa-sha2-nistp256"), SshTestEncoding.Name("nistp256"), SshTestEncoding.String(point));
        byte[] bytes = SshTestEncoding.Join(SshTestEncoding.Name("ecdsa-sha2-nistp256"), SshTestEncoding.String(blob), SshTestEncoding.String([]), new byte[32]);
        ArrangeBytes($"ecdsa-sha2-nistp256 host key whose point is {Convert.ToHexString(point)}", bytes);

        AssertDecoded(false, DecodeOrReport(bytes, "malformed point"));
    }

    [TestMethod]
    public void TryDecodeHostKeySignature_EcdsaKeyWhosePointIsNotOnTheCurve_NeverThrows()
    {
        byte[] point = [0x04, .. Enumerable.Repeat((byte)0x01, 64)];
        byte[] blob = SshTestEncoding.Join(SshTestEncoding.Name("ecdsa-sha2-nistp256"), SshTestEncoding.Name("nistp256"), SshTestEncoding.String(point));
        TestHostKey real = TestHostKey.For("ecdsa-sha2-nistp256");
        byte[] bytes = SshTestEncoding.Join(SshTestEncoding.Name("ecdsa-sha2-nistp256"), SshTestEncoding.String(blob), SshTestEncoding.String(real.Sign(new byte[32])), new byte[32]);
        ArrangeBytesSummary("ecdsa-sha2-nistp256 host key whose point (1, 1) is not on P-256", bytes);

        DecodeOrReport(bytes, "point not on the curve");

        AssertDecoded("no exception", "no exception");
    }

    [TestMethod]
    public void TryDecodeHostKeySignature_EcdsaKeyNamingAnotherCurve_ReturnsFalse()
    {
        TestHostKey key = TestHostKey.For("ecdsa-sha2-nistp256");
        byte[] blob = key.Blob.ToArray();
        int nameLength = BinaryPrimitives.ReadInt32BigEndian(blob);
        int curveStart = 4 + nameLength + 4;
        Encoding.ASCII.GetBytes("nistp384").CopyTo(blob, curveStart);
        byte[] bytes = SshTestEncoding.Join(SshTestEncoding.Name("ecdsa-sha2-nistp256"), SshTestEncoding.String(blob), SshTestEncoding.String(key.Sign(new byte[32])), new byte[32]);
        ArrangeBytesSummary("ecdsa-sha2-nistp256 host key whose curve identifier says nistp384", bytes);

        AssertDecoded(false, DecodeOrReport(bytes, "curve mismatch"));
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty name")]
    [DataRow("SSH-ED25519", DisplayName = "upper case")]
    [DataRow("ssh-ed25519\0", DisplayName = "trailing NUL")]
    [DataRow("ssh-ed25519 ", DisplayName = "trailing space")]
    public void TryDecodeHostKeySignature_AlgorithmNameOneCharacterOff_ReturnsFalse(string name)
    {
        TestHostKey key = TestHostKey.For("ssh-ed25519");
        byte[] bytes = SshTestEncoding.Join(SshTestEncoding.Name(name), SshTestEncoding.String(key.Blob), SshTestEncoding.String(key.Sign(new byte[32])), new byte[32]);
        ArrangeBytesSummary($"host-key algorithm \"{name}\"", bytes);

        AssertDecoded(false, SshWireDecoders.TryDecodeHostKeySignature(bytes));
    }

    // ---- Boundaries and malformed input: compressed payloads ----

    [TestMethod]
    public void TryInflatePayload_PayloadOfExactlyTheLargestSize_ReturnsTrue()
    {
        byte[] compressed = ZlibFirstPacket(new byte[LargestPayload]);
        ArrangeBytes($"zlib stream inflating to {LargestPayload} zero bytes", compressed);

        AssertDecoded(true, SshWireDecoders.TryInflatePayload(compressed));
    }

    [TestMethod]
    public void TryInflatePayload_PayloadOneBytePastTheLargestSize_ReturnsFalse()
    {
        byte[] compressed = ZlibFirstPacket(new byte[LargestPayload + 1]);
        ArrangeBytes($"zlib stream inflating to {LargestPayload + 1} zero bytes", compressed);

        AssertDecoded(false, SshWireDecoders.TryInflatePayload(compressed));
    }

    [TestMethod]
    public void TryInflatePayload_BombInflatingToTenMebibytes_ReturnsFalse()
    {
        byte[] compressed = ZlibFirstPacket(new byte[10 * 1024 * 1024]);
        ArrangeBytes("zlib stream inflating to 10 MiB of zero bytes", compressed);

        AssertDecoded(false, SshWireDecoders.TryInflatePayload(compressed));
    }

    [TestMethod]
    public void TryInflatePayload_EmptyBytes_ReturnsFalse()
    {
        ArrangeBytes("no bytes", []);

        AssertDecoded(false, SshWireDecoders.TryInflatePayload(ReadOnlyMemory<byte>.Empty));
    }

    [TestMethod]
    public void TryInflatePayload_StreamThatInflatesToNothing_ReturnsFalse()
    {
        byte[] compressed = ZlibFirstPacket([]);
        ArrangeBytes("zlib stream with an empty sync-flushed block", compressed);

        AssertDecoded(false, SshWireDecoders.TryInflatePayload(compressed));
    }

    [TestMethod]
    public void TryInflatePayload_ZlibHeaderWithABadCheckValue_ReturnsFalse()
    {
        byte[] compressed = ZlibFirstPacket([2, 1, 2, 3]);
        compressed[1] ^= 0x01;
        ArrangeBytes("zlib stream whose header check bits are one off", compressed);

        AssertDecoded(false, SshWireDecoders.TryInflatePayload(compressed));
    }

    [TestMethod]
    public void TryInflatePayload_SeededRandomBytesAfterAValidHeader_NeverThrow()
    {
        const int Seed = 1950;
        Random random = new(Seed);
        Diagnostics.Arrange("seed", Seed);

        for (int round = 0; round < 500; round++)
        {
            byte[] bytes = [0x78, 0x9C, .. Enumerable.Range(0, random.Next(0, 40)).Select(_ => (byte)random.Next(256))];

            try
            {
                SshWireDecoders.TryInflatePayload(bytes);
            }
            catch (Exception exception)
            {
                Diagnostics.Assert($"seed {Seed}, round {round}", "no exception", exception.GetType().Name);
                Diagnostics.Bytes("input", bytes);
                throw;
            }
        }

        AssertDecoded("no exception", "no exception");
    }

    // ---- State and concurrency ----

    [TestMethod]
    public async Task CountWholePacketsAsync_SameBytesOnSixteenTasksAtOnce_CountTheSameAsOneAtATime()
    {
        byte[] bytes = new SshServerScript().Packet(2, 1).Packet(4).Packet(94, 7, 7).RawPacket(13, 4, new byte[16]).Bytes;
        int alone = await SshWireDecoders.CountWholePacketsAsync(bytes, CancellationToken.None);
        Diagnostics.Arrange("wire", $"three packets then a misaligned one; alone it counts {alone}");

        int[] counts = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(async () => await SshWireDecoders.CountWholePacketsAsync(bytes, CancellationToken.None))));

        Diagnostics.Assert("every concurrent count", $"{alone} x16", string.Join(",", counts));
        Assert.AreEqual(3, alone);
        Assert.IsTrue(counts.All(count => count == alone));
    }

    [TestMethod]
    public void TryInflatePayload_SameFirstPacketTwice_InflatesBothTimes()
    {
        byte[] compressed = ZlibFirstPacket([2, 1, 2, 3]);
        ArrangeBytes("the same first compressed packet, read twice", compressed);

        bool first = SshWireDecoders.TryInflatePayload(compressed);
        bool second = SshWireDecoders.TryInflatePayload(compressed);

        Diagnostics.Assert("first, second", "True, True", $"{first}, {second}");
        Assert.IsTrue(first);
        Assert.IsTrue(second);
    }

    [TestMethod]
    public void TryDecodeKexInit_RefusedInputThenAWholeOne_ReadsTheWholeOne()
    {
        byte[] payload = KexInit(Enumerable.Repeat("x", 10).ToArray());
        Diagnostics.Arrange("calls", "a cut KEXINIT, then the whole one");

        bool refused = SshWireDecoders.TryDecodeKexInit(payload.AsMemory(0, 10));
        bool read = SshWireDecoders.TryDecodeKexInit(payload);

        Diagnostics.Assert("cut, whole", "False, True", $"{refused}, {read}");
        Assert.IsFalse(refused);
        Assert.IsTrue(read);
    }

    [TestMethod]
    public void TryDecodeHostKeySignature_EveryAlgorithmOnManyTasksAtOnce_DecodesTheSameAsOneAtATime()
    {
        byte[] exchangeHash = new byte[32];
        byte[][] inputs = [.. HostKeyAlgorithms.Select(algorithm =>
        {
            TestHostKey key = TestHostKey.For(algorithm);
            return SshTestEncoding.Join(SshTestEncoding.Name(algorithm), SshTestEncoding.String(key.Blob), SshTestEncoding.String(key.Sign(exchangeHash)), exchangeHash);
        })];
        Diagnostics.Arrange("inputs", $"{inputs.Length} algorithms x 8 tasks each");

        bool[] results = [.. Enumerable.Range(0, inputs.Length * 8).AsParallel().Select(index => SshWireDecoders.TryDecodeHostKeySignature(inputs[index % inputs.Length]))];

        Diagnostics.Assert("every concurrent decode", "all True", results.All(result => result) ? "all True" : "some False");
        Assert.IsTrue(results.All(result => result));
    }

    public static IEnumerable<object[]> HostKeyAlgorithmRows => HostKeyAlgorithms.Select(algorithm => new object[] { algorithm });

    private static byte[] PacketOfLength(uint packetLength, byte paddingLength)
    {
        byte[] rest = new byte[packetLength - 1];
        rest[0] = 2;
        return new SshServerScript().RawPacket(packetLength, paddingLength, rest).Bytes;
    }

    private static byte[] KexInit(string[] nameLists)
    {
        byte[][] fields = [[20], new byte[16], .. nameLists.Select(SshTestEncoding.Name), [0], SshTestEncoding.UInt32(0)];
        return SshTestEncoding.Join(fields);
    }

    // A zlib stream's first sync-flushed piece, as the first compressed SSH packet carries it.
    private static byte[] ZlibFirstPacket(byte[] payload)
    {
        using MemoryStream output = new();
        using (ZLibStream deflater = new(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflater.Write(payload);
            deflater.Flush();
            return output.ToArray();
        }
    }

    // Where a host key blob's key ends. A certificate is read only as far as its certified
    // key - its name, nonce and key fields - as libssh2 1.11.1 reads one; its principals,
    // validity and CA signature after that are not the signature check's to read.
    private static int KeyEnd(string algorithm, byte[] blob)
    {
        if (!algorithm.Contains("-cert-", StringComparison.Ordinal))
        {
            return blob.Length;
        }

        int offset = 0;
        for (int field = 0; field < 3; field++)
        {
            offset += 4 + BinaryPrimitives.ReadInt32BigEndian(blob.AsSpan(offset));
        }

        return offset;
    }

    // The RSA blob's fields again, with three zero bytes before the exponent and modulus.
    private static byte[] PadRsaMpints(byte[] blob)
    {
        int offset = 4 + BinaryPrimitives.ReadInt32BigEndian(blob);
        List<byte> padded = [.. blob[..offset]];
        for (int field = 0; field < 2; field++)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(blob.AsSpan(offset));
            padded.AddRange(SshTestEncoding.String([0, 0, 0, .. blob.AsSpan(offset + 4, length)]));
            offset += 4 + length;
        }

        return [.. padded];
    }

    private static byte[] Mutate(byte[] bytes, Random random)
    {
        byte[] mutated = bytes.ToArray();
        switch (random.Next(4))
        {
            case 0:
                mutated[random.Next(mutated.Length)] ^= (byte)(1 << random.Next(8));
                return mutated;
            case 1:
                mutated[random.Next(mutated.Length)] = (byte)random.Next(256);
                return mutated;
            case 2:
                // Rewrite one big-endian length-shaped word with a hostile value.
                int at = random.Next(Math.Max(1, mutated.Length - 3));
                uint hostile = random.Next(3) switch { 0 => 0u, 1 => 0xFFFFFFFFu, _ => (uint)random.Next(0, 600) };
                if (at + 4 <= mutated.Length)
                {
                    BinaryPrimitives.WriteUInt32BigEndian(mutated.AsSpan(at), hostile);
                }

                return mutated;
            default:
                return mutated[..random.Next(mutated.Length)];
        }
    }

    private async Task<int> CountOrReport(byte[] bytes, int seed, int round)
    {
        try
        {
            return await SshWireDecoders.CountWholePacketsAsync(bytes, CancellationToken.None);
        }
        catch (Exception exception)
        {
            Diagnostics.Assert($"seed {seed}, round {round}", "no exception", exception.GetType().Name);
            Diagnostics.Bytes("input", bytes);
            throw;
        }
    }

    private bool DecodeOrReport(byte[] bytes, string label)
    {
        try
        {
            return SshWireDecoders.TryDecodeHostKeySignature(bytes);
        }
        catch (Exception exception)
        {
            Diagnostics.Assert(label, "no exception", $"{exception.GetType().Name}: {exception.Message}");
            Diagnostics.Bytes("input", bytes);
            throw;
        }
    }

    private void ArrangeBytes(string label, byte[] bytes)
    {
        Diagnostics.Arrange(label, $"{bytes.Length} bytes");
        Diagnostics.Bytes(label, bytes);
    }

    private void ArrangeBytesSummary(string label, byte[] bytes) =>
        Diagnostics.Arrange(label, $"{bytes.Length} bytes, starting {Convert.ToHexString(bytes.AsSpan(0, Math.Min(16, bytes.Length)))}");

    private void AssertCount(int expected, int actual)
    {
        Diagnostics.Act("whole packets", actual);
        Diagnostics.Assert("whole packets", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    private void AssertDecoded(object expected, object actual)
    {
        Diagnostics.Act("result", actual);
        Diagnostics.Assert("result", expected, actual);
        Assert.AreEqual(expected, actual);
    }
}
