using System.Security.Cryptography;
using Curl.Protocol.Ssh.Authentication;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.KeyExchange;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Ssh.PacketProtection;
using Curl.Testing;

namespace Curl.Protocol.Ssh.Transport;

[TestClass]
public sealed class SshPacketWriterTests
{
    public TestContext TestContext { get; set; } = null!;

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
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("payload length", payloadLength);

        int padding = SshPacketWriter.PaddingLengthFor(payloadLength);
        diagnostics.Act("padding length", padding);

        diagnostics.Assert("padding length", expected, padding);
        Assert.AreEqual(expected, padding);
    }

    [TestMethod]
    public void PaddingLengthFor_EveryPayloadLength_GivesAWholeNumberOfBlocksAndFourToElevenBytes()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("payload lengths", "0 to 63");

        for (int payloadLength = 0; payloadLength < 64; payloadLength++)
        {
            int padding = SshPacketWriter.PaddingLengthFor(payloadLength);
            int remainder = (4 + 1 + payloadLength + padding) % 8;
            if (payloadLength is 0 or 63 || padding is < 4 or > 11 || remainder != 0)
            {
                diagnostics.Act($"padding for payload {payloadLength}", padding);
            }

            diagnostics.Assert($"block remainder for payload {payloadLength}", 0, remainder);
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
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("payload length", payloadLength);
        diagnostics.Arrange("pads packet length field", padsPacketLengthField);

        int padding = SshPacketWriter.PaddingLengthFor(payloadLength, 16, padsPacketLengthField);
        int remainder = ((padsPacketLengthField ? 4 : 0) + 1 + payloadLength + padding) % 16;
        diagnostics.Act("padding length", padding);
        diagnostics.Act("block remainder", remainder);

        diagnostics.Assert("padding length", expected, padding);
        diagnostics.Assert("block remainder", 0, remainder);
        Assert.AreEqual(expected, padding);
        Assert.AreEqual(0, ((padsPacketLengthField ? 4 : 0) + 1 + payloadLength + padding) % 16);
    }

    [TestMethod]
    public async Task WriteAsync_AfterChangeProtection_SealsWithTheNewKeysAndSequenceNumber()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        SshKeyDerivation keys = new(HashAlgorithmName.SHA256, [9], [.. new byte[32]], [.. new byte[32]]);
        SshNegotiatedAlgorithms algorithms = SshTestAlgorithms.With("aes128-ctr", "hmac-sha2-256-etm@openssh.com");
        diagnostics.Arrange("cipher", "aes128-ctr");
        diagnostics.Arrange("mac", "hmac-sha2-256-etm@openssh.com");
        ScriptedConnection connection = new();
        SshPacketWriter writer = new(connection, new RepeatingRandomSource(0x5A));
        await writer.WriteAsync(new byte[] { 21 }, CancellationToken.None);

        writer.ChangeProtection(SshPacketProtections.ForClientToServer(algorithms, keys));
        await writer.WriteAsync(new byte[] { 5, 0xAB }, CancellationToken.None);
        diagnostics.Bytes("written", connection.Written);

        using ISshPacketProtection expected = SshPacketProtections.ForClientToServer(algorithms, keys);
        byte[] packet = [0, 0, 0, 16, 13, 5, 0xAB, .. Enumerable.Repeat((byte)0x5A, 13)];
        byte[] expectedSealed = expected.Seal(1, packet);
        byte[] actualSealed = connection.Written[16..];
        diagnostics.Act("sealed second packet", SshAuthenticationDiagnostics.MessageName([5]));
        diagnostics.Bytes("sealed second packet", actualSealed);

        diagnostics.Diff("sealed second packet", expectedSealed, actualSealed);
        CollectionAssert.AreEqual(expectedSealed, actualSealed);
    }

    [TestMethod]
    public async Task WriteAsync_FramesLengthPaddingPayloadAndRandomPadding()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Bytes("payload", [2, 0xAB]);
        diagnostics.Arrange("random byte", 0x5A);
        ScriptedConnection connection = new();
        SshPacketWriter writer = new(connection, new RepeatingRandomSource(0x5A));

        await writer.WriteAsync(new byte[] { 2, 0xAB }, CancellationToken.None);
        byte[] expected = [0, 0, 0, 12, 9, 2, 0xAB, 0x5A, 0x5A, 0x5A, 0x5A, 0x5A, 0x5A, 0x5A, 0x5A, 0x5A];
        diagnostics.Bytes("written", connection.Written);
        diagnostics.Act("flush count", connection.FlushCount);

        diagnostics.Diff("written", expected, connection.Written);
        diagnostics.Assert("flush count", 1, connection.FlushCount);
        CollectionAssert.AreEqual(expected, connection.Written);
        Assert.AreEqual(1, connection.FlushCount);
    }

    [TestMethod]
    public async Task WriteAsync_CountsSequenceNumbersFromZero()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("packets to write", 2);
        SshPacketWriter writer = new(new ScriptedConnection(), new RepeatingRandomSource(0));

        uint before = writer.SequenceNumber;
        await writer.WriteAsync(new byte[] { 2 }, CancellationToken.None);
        await writer.WriteAsync(new byte[] { 2 }, CancellationToken.None);
        uint after = writer.SequenceNumber;
        diagnostics.Act("sequence number before", before);
        diagnostics.Act("sequence number after", after);

        diagnostics.Assert("sequence number before", 0u, before);
        diagnostics.Assert("sequence number after", 2u, after);
        Assert.AreEqual(0u, before);
        Assert.AreEqual(2u, after);
    }

    [TestMethod]
    public async Task WriteAsync_ConnectionReset_ThrowsSshConnectionLostAndKeepsTheSequenceNumber()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("reset on write", true);
        SshPacketWriter writer = new(new ResettingConnection(true), new RepeatingRandomSource(0));

        SshConnectionLostException lost = await Assert.ThrowsExactlyAsync<SshConnectionLostException>(async () => await writer.WriteAsync(new byte[] { 2 }, CancellationToken.None));
        diagnostics.Act("exception", $"{lost.GetType().Name}: {SshAuthenticationDiagnostics.Text(lost.Message)}");
        diagnostics.Act("inner exception", lost.InnerException?.GetType().Name ?? "(null)");
        diagnostics.Act("sequence number", writer.SequenceNumber);

        diagnostics.Assert("inner exception type", nameof(IOException), lost.InnerException?.GetType().Name ?? "(null)");
        diagnostics.Assert("sequence number", 0u, writer.SequenceNumber);
        Assert.IsInstanceOfType<IOException>(lost.InnerException);
        Assert.AreEqual(0u, writer.SequenceNumber);
    }
}
