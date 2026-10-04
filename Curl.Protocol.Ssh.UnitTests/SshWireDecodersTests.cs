using Curl.Protocol.Ssh.Compression;
using Curl.Protocol.Ssh.Fakes;

namespace Curl.Protocol.Ssh;

[TestClass]
public sealed class SshWireDecodersTests
{
    private static readonly byte[] EmptyKexInit = [20, .. new byte[16], .. new byte[10 * 4], 0, 0, 0, 0, 0];

    [TestMethod]
    public async Task CountWholePacketsAsync_PacketsThenTheEnd_CountsEachPacket()
    {
        byte[] bytes = new SshServerScript().Packet(2, 1, 2, 3).Packet(4).Bytes;

        Assert.AreEqual(2, await SshWireDecoders.CountWholePacketsAsync(bytes, CancellationToken.None));
    }

    [TestMethod]
    public async Task CountWholePacketsAsync_PacketCutShort_CountsThePacketsBeforeIt()
    {
        byte[] bytes = new SshServerScript().Packet(2, 1).Packet(4).Bytes;

        Assert.AreEqual(1, await SshWireDecoders.CountWholePacketsAsync(bytes.AsMemory(0, bytes.Length - 1), CancellationToken.None));
    }

    [TestMethod]
    [DataRow(13u, (byte)4, DisplayName = "broken framing")]
    [DataRow(0u, (byte)4, DisplayName = "zero length")]
    public async Task CountWholePacketsAsync_RefusedPacket_CountsThePacketsBeforeIt(uint packetLength, byte paddingLength)
    {
        byte[] bytes = new SshServerScript().Packet(2).RawPacket(packetLength, paddingLength, new byte[64]).Bytes;

        Assert.AreEqual(1, await SshWireDecoders.CountWholePacketsAsync(bytes, CancellationToken.None));
    }

    [TestMethod]
    public void TryInflatePayload_CompressedPayload_ReturnsTrue()
    {
        byte[] compressed = new SshZlibCompressor().Compress([2, 1, 2, 3]);

        Assert.IsTrue(SshWireDecoders.TryInflatePayload(compressed));
    }

    [TestMethod]
    public void TryInflatePayload_BytesThatAreNotZlib_ReturnsFalse() =>
        Assert.IsFalse(SshWireDecoders.TryInflatePayload(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }));

    [TestMethod]
    public void TryDecodeKexInit_WholeKexInit_ReturnsTrue() =>
        Assert.IsTrue(SshWireDecoders.TryDecodeKexInit(EmptyKexInit));

    [TestMethod]
    public void TryDecodeKexInit_KexInitCutShort_ReturnsFalse() =>
        Assert.IsFalse(SshWireDecoders.TryDecodeKexInit(EmptyKexInit.AsMemory(0, 20)));

    [TestMethod]
    public void TryDecodeSftpAttributes_SizeOnly_ReturnsTrue() =>
        Assert.IsTrue(SshWireDecoders.TryDecodeSftpAttributes(new byte[] { 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 9 }));

    [TestMethod]
    public void TryDecodeSftpAttributes_SizeCutShort_ReturnsFalse() =>
        Assert.IsFalse(SshWireDecoders.TryDecodeSftpAttributes(new byte[] { 0, 0, 0, 1, 0, 0 }));

    [TestMethod]
    public void TryDecodeHostKeySignature_SignedExchangeHash_ReturnsTrue()
    {
        TestHostKey key = TestHostKey.Ed25519();
        byte[] exchangeHash = [.. Enumerable.Range(0, 32).Select(value => (byte)value)];
        byte[] bytes = SshTestEncoding.Join(
            SshTestEncoding.Name(key.Algorithm), SshTestEncoding.String(key.Blob), SshTestEncoding.String(key.Sign(exchangeHash)), exchangeHash);

        Assert.IsTrue(SshWireDecoders.TryDecodeHostKeySignature(bytes));
    }

    [TestMethod]
    public void TryDecodeHostKeySignature_SignatureCutShort_ReturnsFalse()
    {
        TestHostKey key = TestHostKey.Ed25519();
        byte[] bytes = SshTestEncoding.Join(SshTestEncoding.Name(key.Algorithm), SshTestEncoding.String(key.Blob), SshTestEncoding.UInt32(64));

        Assert.IsFalse(SshWireDecoders.TryDecodeHostKeySignature(bytes));
    }

    [TestMethod]
    public void TryDecodeHostKeySignature_UnimplementedAlgorithm_ReturnsFalse() =>
        Assert.IsFalse(SshWireDecoders.TryDecodeHostKeySignature(SshTestEncoding.Name("ssh-unknown")));

    [TestMethod]
    public async Task SshByteArrayConnection_WritesAreDiscardedAndItHasNoAddress()
    {
        await using Transport.SshByteArrayConnection connection = new(new byte[] { 1 });

        await connection.WriteAsync(new byte[] { 2 }, CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);

        Assert.IsFalse(connection.IsSecure);
        Assert.IsNull(connection.RemoteEndPoint);
        byte[] buffer = new byte[4];
        Assert.AreEqual(1, await connection.ReadAsync(buffer, CancellationToken.None));
        Assert.AreEqual(0, await connection.ReadAsync(buffer, CancellationToken.None));
    }
}
