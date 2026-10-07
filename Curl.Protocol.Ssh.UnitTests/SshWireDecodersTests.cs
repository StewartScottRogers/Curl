using Curl.Protocol.Ssh.Compression;
using Curl.Protocol.Ssh.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ssh;

[TestClass]
public sealed class SshWireDecodersTests
{
    private static readonly byte[] EmptyKexInit = [20, .. new byte[16], .. new byte[10 * 4], 0, 0, 0, 0, 0];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task CountWholePacketsAsync_PacketsThenTheEnd_CountsEachPacket()
    {
        byte[] bytes = new SshServerScript().Packet(2, 1, 2, 3).Packet(4).Bytes;
        ArrangeBytes("wire: messages 2 and 4, then the end", bytes);

        int count = await SshWireDecoders.CountWholePacketsAsync(bytes, CancellationToken.None);

        AssertResult("whole packets", 2, count);
        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public async Task CountWholePacketsAsync_PacketCutShort_CountsThePacketsBeforeIt()
    {
        byte[] bytes = new SshServerScript().Packet(2, 1).Packet(4).Bytes;
        ArrangeBytes("wire: messages 2 and 4, the last byte cut", bytes.AsSpan(0, bytes.Length - 1).ToArray());

        int count = await SshWireDecoders.CountWholePacketsAsync(bytes.AsMemory(0, bytes.Length - 1), CancellationToken.None);

        AssertResult("whole packets", 1, count);
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    [DataRow(13u, (byte)4, DisplayName = "broken framing")]
    [DataRow(0u, (byte)4, DisplayName = "zero length")]
    public async Task CountWholePacketsAsync_RefusedPacket_CountsThePacketsBeforeIt(uint packetLength, byte paddingLength)
    {
        byte[] bytes = new SshServerScript().Packet(2).RawPacket(packetLength, paddingLength, new byte[64]).Bytes;
        ArrangeBytes($"wire: message 2, then packet_length {packetLength}, padding_length {paddingLength}", bytes);

        int count = await SshWireDecoders.CountWholePacketsAsync(bytes, CancellationToken.None);

        AssertResult("whole packets", 1, count);
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public void TryInflatePayload_CompressedPayload_ReturnsTrue()
    {
        byte[] compressed = new SshZlibCompressor().Compress([2, 1, 2, 3]);
        ArrangeBytes("zlib-compressed payload 02 01 02 03", compressed);

        bool inflated = SshWireDecoders.TryInflatePayload(compressed);

        AssertResult("TryInflatePayload", true, inflated);
        Assert.IsTrue(inflated);
    }

    [TestMethod]
    public void TryInflatePayload_BytesThatAreNotZlib_ReturnsFalse()
    {
        byte[] bytes = [0xFF, 0xFF, 0xFF, 0xFF];
        ArrangeBytes("not zlib", bytes);

        bool inflated = SshWireDecoders.TryInflatePayload(bytes);

        AssertResult("TryInflatePayload", false, inflated);
        Assert.IsFalse(inflated);
    }

    [TestMethod]
    public void TryDecodeKexInit_WholeKexInit_ReturnsTrue()
    {
        ArrangeBytes("empty SSH_MSG_KEXINIT", EmptyKexInit);

        bool decoded = SshWireDecoders.TryDecodeKexInit(EmptyKexInit);

        AssertResult("TryDecodeKexInit", true, decoded);
        Assert.IsTrue(decoded);
    }

    [TestMethod]
    public void TryDecodeKexInit_KexInitCutShort_ReturnsFalse()
    {
        ArrangeBytes("empty SSH_MSG_KEXINIT, first 20 bytes", EmptyKexInit[..20]);

        bool decoded = SshWireDecoders.TryDecodeKexInit(EmptyKexInit.AsMemory(0, 20));

        AssertResult("TryDecodeKexInit", false, decoded);
        Assert.IsFalse(decoded);
    }

    [TestMethod]
    public void TryDecodeSftpAttributes_SizeOnly_ReturnsTrue()
    {
        byte[] bytes = [0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 9];
        ArrangeBytes("SFTP attributes: flags SIZE, size 9", bytes);

        bool decoded = SshWireDecoders.TryDecodeSftpAttributes(bytes);

        AssertResult("TryDecodeSftpAttributes", true, decoded);
        Assert.IsTrue(decoded);
    }

    [TestMethod]
    public void TryDecodeSftpAttributes_SizeCutShort_ReturnsFalse()
    {
        byte[] bytes = [0, 0, 0, 1, 0, 0];
        ArrangeBytes("SFTP attributes: flags SIZE, size cut to 2 bytes", bytes);

        bool decoded = SshWireDecoders.TryDecodeSftpAttributes(bytes);

        AssertResult("TryDecodeSftpAttributes", false, decoded);
        Assert.IsFalse(decoded);
    }

    [TestMethod]
    public void TryDecodeHostKeySignature_SignedExchangeHash_ReturnsTrue()
    {
        TestHostKey key = TestHostKey.Ed25519();
        byte[] exchangeHash = [.. Enumerable.Range(0, 32).Select(value => (byte)value)];
        byte[] bytes = SshTestEncoding.Join(
            SshTestEncoding.Name(key.Algorithm), SshTestEncoding.String(key.Blob), SshTestEncoding.String(key.Sign(exchangeHash)), exchangeHash);
        ArrangeBytes($"{key.Algorithm} host key, signature and exchange hash", bytes);

        bool decoded = SshWireDecoders.TryDecodeHostKeySignature(bytes);

        AssertResult("TryDecodeHostKeySignature", true, decoded);
        Assert.IsTrue(decoded);
    }

    [TestMethod]
    public void TryDecodeHostKeySignature_SignatureCutShort_ReturnsFalse()
    {
        TestHostKey key = TestHostKey.Ed25519();
        byte[] bytes = SshTestEncoding.Join(SshTestEncoding.Name(key.Algorithm), SshTestEncoding.String(key.Blob), SshTestEncoding.UInt32(64));
        ArrangeBytes($"{key.Algorithm} host key, then a 64-byte signature length and no signature", bytes);

        bool decoded = SshWireDecoders.TryDecodeHostKeySignature(bytes);

        AssertResult("TryDecodeHostKeySignature", false, decoded);
        Assert.IsFalse(decoded);
    }

    [TestMethod]
    public void TryDecodeHostKeySignature_UnimplementedAlgorithm_ReturnsFalse()
    {
        byte[] bytes = SshTestEncoding.Name("ssh-unknown");
        ArrangeBytes("algorithm name ssh-unknown", bytes);

        bool decoded = SshWireDecoders.TryDecodeHostKeySignature(bytes);

        AssertResult("TryDecodeHostKeySignature", false, decoded);
        Assert.IsFalse(decoded);
    }

    [TestMethod]
    public async Task SshByteArrayConnection_WritesAreDiscardedAndItHasNoAddress()
    {
        await using Transport.SshByteArrayConnection connection = new(new byte[] { 1 });
        Diagnostics.Arrange("connection bytes", "01");

        await connection.WriteAsync(new byte[] { 2 }, CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);

        Diagnostics.Act("IsSecure, RemoteEndPoint", $"{connection.IsSecure}, {connection.RemoteEndPoint?.ToString() ?? "(null)"}");
        Diagnostics.Assert("IsSecure, RemoteEndPoint", "False, (null)", $"{connection.IsSecure}, {connection.RemoteEndPoint?.ToString() ?? "(null)"}");
        Assert.IsFalse(connection.IsSecure);
        Assert.IsNull(connection.RemoteEndPoint);
        byte[] buffer = new byte[4];
        int first = await connection.ReadAsync(buffer, CancellationToken.None);
        Diagnostics.Assert("first read", 1, first);
        Assert.AreEqual(1, first);
        int second = await connection.ReadAsync(buffer, CancellationToken.None);
        Diagnostics.Assert("second read", 0, second);
        Assert.AreEqual(0, second);
    }

    private void ArrangeBytes(string label, byte[] bytes)
    {
        Diagnostics.Arrange(label, $"{bytes.Length} bytes");
        Diagnostics.Bytes(label, bytes);
    }

    private void AssertResult(string label, object expected, object actual)
    {
        Diagnostics.Act(label, actual);
        Diagnostics.Assert(label, expected, actual);
    }
}
