using System.Security.Cryptography;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.KeyExchange;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Ssh.PacketProtection;

namespace Curl.Protocol.Ssh.Transport;

[TestClass]
public sealed class SshPacketWriterTests
{
    [TestMethod]
    [DataRow(1072, 11, DisplayName = "the Windows reference KEXINIT, measured")]
    [DataRow(21, 6, DisplayName = "libssh2's DISCONNECT Shutdown, measured")]
    [DataRow(0, 11)]
    [DataRow(3, 8)]
    [DataRow(6, 5)]
    [DataRow(7, 4)]
    [DataRow(8, 11)]
    public void PaddingLengthFor_IsTheFewestBytesAtLeastFourThatAlignToEight(int payloadLength, int expected)
    {
        Assert.AreEqual(expected, SshPacketWriter.PaddingLengthFor(payloadLength));
    }

    [TestMethod]
    public void PaddingLengthFor_EveryPayloadLength_GivesAWholeNumberOfBlocksAndFourToElevenBytes()
    {
        for (int payloadLength = 0; payloadLength < 64; payloadLength++)
        {
            int padding = SshPacketWriter.PaddingLengthFor(payloadLength);
            Assert.IsTrue(padding is >= 4 and <= 11, $"payload {payloadLength}: padding {padding}");
            Assert.AreEqual(0, (4 + 1 + payloadLength + padding) % 8, $"payload {payloadLength}");
        }
    }

    [TestMethod]
    [DataRow(0, true, 11, DisplayName = "MAC-then-encrypt, 16-byte block: length field counted")]
    [DataRow(10, true, 17, DisplayName = "MAC-then-encrypt, three short of a block: another block")]
    [DataRow(0, false, 15, DisplayName = "encrypt-then-MAC or AES-GCM: length field not counted")]
    [DataRow(11, false, 4, DisplayName = "encrypt-then-MAC or AES-GCM: exactly four")]
    [DataRow(12, false, 19, DisplayName = "encrypt-then-MAC or AES-GCM: three short of a block")]
    public void PaddingLengthFor_SixteenByteBlocks_AlignsThePaddedPart(int payloadLength, bool padsPacketLengthField, int expected)
    {
        int padding = SshPacketWriter.PaddingLengthFor(payloadLength, 16, padsPacketLengthField);

        Assert.AreEqual(expected, padding);
        Assert.AreEqual(0, ((padsPacketLengthField ? 4 : 0) + 1 + payloadLength + padding) % 16);
    }

    [TestMethod]
    public async Task WriteAsync_AfterChangeProtection_SealsWithTheNewKeysAndSequenceNumber()
    {
        SshKeyDerivation keys = new(HashAlgorithmName.SHA256, [9], [.. new byte[32]], [.. new byte[32]]);
        SshNegotiatedAlgorithms algorithms = SshTestAlgorithms.With("aes128-ctr", "hmac-sha2-256-etm@openssh.com");
        ScriptedConnection connection = new();
        SshPacketWriter writer = new(connection, new RepeatingRandomSource(0x5A));
        await writer.WriteAsync(new byte[] { 21 }, CancellationToken.None);

        writer.ChangeProtection(SshPacketProtections.ForClientToServer(algorithms, keys));
        await writer.WriteAsync(new byte[] { 5, 0xAB }, CancellationToken.None);

        using ISshPacketProtection expected = SshPacketProtections.ForClientToServer(algorithms, keys);
        byte[] packet = [0, 0, 0, 16, 13, 5, 0xAB, .. Enumerable.Repeat((byte)0x5A, 13)];
        CollectionAssert.AreEqual(expected.Seal(1, packet), connection.Written[16..]);
    }

    [TestMethod]
    public async Task WriteAsync_FramesLengthPaddingPayloadAndRandomPadding()
    {
        ScriptedConnection connection = new();
        SshPacketWriter writer = new(connection, new RepeatingRandomSource(0x5A));

        await writer.WriteAsync(new byte[] { 2, 0xAB }, CancellationToken.None);

        CollectionAssert.AreEqual(
            new byte[] { 0, 0, 0, 12, 9, 2, 0xAB, 0x5A, 0x5A, 0x5A, 0x5A, 0x5A, 0x5A, 0x5A, 0x5A, 0x5A },
            connection.Written);
        Assert.AreEqual(1, connection.FlushCount);
    }

    [TestMethod]
    public async Task WriteAsync_CountsSequenceNumbersFromZero()
    {
        SshPacketWriter writer = new(new ScriptedConnection(), new RepeatingRandomSource(0));

        Assert.AreEqual(0u, writer.SequenceNumber);
        await writer.WriteAsync(new byte[] { 2 }, CancellationToken.None);
        await writer.WriteAsync(new byte[] { 2 }, CancellationToken.None);

        Assert.AreEqual(2u, writer.SequenceNumber);
    }
}
