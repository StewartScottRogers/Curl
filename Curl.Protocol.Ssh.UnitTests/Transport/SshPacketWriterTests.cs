using Curl.Protocol.Ssh.Fakes;

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
