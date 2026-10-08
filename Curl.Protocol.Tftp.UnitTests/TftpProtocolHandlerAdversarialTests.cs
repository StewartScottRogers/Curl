using System.Globalization;
using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Tftp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Attacks <see cref="TftpProtocolHandler" /> through its public surface only, by the
/// method in <c>Documentation/Wiki/Adversarial-Testing.md</c> (BL-1520): block numbers
/// wrapping past 65535, the largest block size, ERROR and option acknowledgement packets
/// one field wrong, DATA blocks in the invalid partitions of the block number, seeded
/// duplicate and out-of-order delivery, and one handler driven again and side by side.
/// The oracle is curl 8.21.0's <c>tftp.c</c>, whose behaviour the existing suites pin.
/// </summary>
[TestClass]
public sealed class TftpProtocolHandlerAdversarialTests
{
    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 69);

    /// <summary>The server's transfer identifier: the new port it answers from.</summary>
    private static readonly IPEndPoint TransferEndPoint = new(IPAddress.Loopback, 50123);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_BlockSize8DownloadPastBlock65535_WrapsToBlock0AndWritesEveryByteInOrder()
    {
        const int fullBlocks = 65536;
        var script = new List<(byte[] Datagram, EndPoint Source)>(fullBlocks + 2) { OptionAcknowledgement("blksize\08\0") };
        var expected = new byte[fullBlocks * 8];
        for (var index = 0; index < fullBlocks; index++)
        {
            var payload = expected.AsSpan(index * 8, 8);
            payload.Fill((byte)(index % 251));
            script.Add(Data(unchecked((ushort)(index + 1)), payload));
        }

        script.Add(Data(1, []));
        Diagnostics.Arrange("script", "OACK blksize 8, 65536 full 8-byte blocks numbered 1..65535 then 0, then an empty block 1");
        var channel = new ScriptedDatagramChannel(ServerEndPoint, [.. script]);
        var output = new MemoryStream();

        var result = await new TftpProtocolHandler(Connector(channel)).ExecuteAsync(Context(output, tftpBlockSize: 8));

        TftpTestDiagnostics.Result(Diagnostics, result);
        var acknowledged = AcknowledgedBlocks(channel);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("bytes written", expected.Length, (int)output.Length);
        CollectionAssert.AreEqual(expected, output.ToArray());
        Assert.AreEqual(expected.Length, result.BytesTransferred);
        Diagnostics.Assert("acknowledgements", fullBlocks + 2, acknowledged.Length);
        Assert.HasCount(fullBlocks + 2, acknowledged);
        Diagnostics.Assert("ack after block 65535", (ushort)0, acknowledged[fullBlocks]);
        Assert.AreEqual((ushort)0, acknowledged[0]);
        Assert.AreEqual((ushort)65535, acknowledged[fullBlocks - 1]);
        Assert.AreEqual((ushort)0, acknowledged[fullBlocks]);
        Assert.AreEqual((ushort)1, acknowledged[fullBlocks + 1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_BlockSize65464FullBlockThenEmptyBlock_TreatsTheFullBlockAsNotLast()
    {
        var full = new byte[65464];
        full.AsSpan().Fill((byte)'z');
        var channel = Channel(OptionAcknowledgement("blksize\065464\0"), Data(1, full), Data(2, []));
        var output = new MemoryStream();

        var result = await RunAsync(channel, Context(output, tftpBlockSize: 65464));

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("bytes written", 65464L, output.Length);
        Assert.AreEqual(65464, output.Length);
        CollectionAssert.AreEqual(new ushort[] { 0, 1, 2 }, AcknowledgedBlocks(channel));
    }

    /// <summary>
    /// Measured against curl 8.21.0 (Schannel) with Record-CurlExchange.ps1 -Tftp: it
    /// answers the late OACK with ACK 0, ignores DATA 2, writes the next DATA 1 after the
    /// first block (514 bytes), acknowledges it as block 1 and exits 0 (BL-1666).
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_OptionAcknowledgementAfterData1_AcknowledgesBlock0AndWritesTheNextData1AsNewData()
    {
        var channel = Channel(
            Data(1, Payload(512, 'a')),
            OptionAcknowledgement("blksize\0512\0"),
            Data(1, "bb"));
        var output = new MemoryStream();

        var result = await RunAsync(channel, Context(output));

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("bytes written", 514L, output.Length);
        Assert.AreEqual(Payload(512, 'a') + "bb", Encoding.ASCII.GetString(output.ToArray()));
        Assert.AreEqual(514, result.BytesTransferred);
        CollectionAssert.AreEqual(new ushort[] { 1, 0, 1 }, AcknowledgedBlocks(channel));
    }

    [TestMethod]
    public async Task ExecuteAsync_MaxFileSizeExactlyTheFirstBlockAndASecondBlockFollows_WritesTheFirstAndExits63()
    {
        var channel = Channel(Data(1, Payload(512, 'a')), Data(2, Payload(1, 'b')));
        var output = new MemoryStream();
        var context = new TransferContext { Url = CurlUrl.Parse("tftp://h/file.txt"), Output = output, MaxFileSize = 512 };

        var result = await RunAsync(channel, context);

        Diagnostics.Assert("exit code", CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual("Exceeded the maximum allowed file size (512) with 512 bytes", result.ErrorMessage);
        Assert.AreEqual(512, output.Length);
    }

    [TestMethod]
    [DataRow((ushort)8, DisplayName = "first code past curl's table")]
    [DataRow((ushort)65535, DisplayName = "largest code")]
    public async Task ExecuteAsync_ErrorPacketWithACodeCurlHasNoNameFor_ExitsAbortedByCallback(ushort code)
    {
        var channel = Channel(([0, 5, (byte)(code >> 8), (byte)code, .. "x"u8, 0], TransferEndPoint));

        var result = await RunAsync(channel, Context());

        Diagnostics.Assert("exit code", CurlExitCode.AbortedByCallback, result.ExitCode);
        Assert.AreEqual(CurlExitCode.AbortedByCallback, result.ExitCode);
        Assert.AreEqual("Operation was aborted by an application callback", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(new byte[] { 0, 5, 0, 1 }, DisplayName = "no text and no terminator")]
    [DataRow(new byte[] { 0, 5, 0, 1, 0 }, DisplayName = "empty text")]
    [DataRow(new byte[] { 0, 5, 0, 1, (byte)'n', (byte)'o' }, DisplayName = "text without its terminator")]
    [DataRow(new byte[] { 0, 5, 0, 1, 0xFF, 0xFE, 0xC3, 0 }, DisplayName = "text that is not UTF-8")]
    [DataRow(new byte[] { 0, 5, 0, 1, (byte)'a', 0, (byte)'b', 0 }, DisplayName = "text with a NUL inside")]
    public async Task ExecuteAsync_ErrorPacketWithMalformedText_ExitsWithTheMappedCodeWithoutThrowing(byte[] packet)
    {
        var channel = Channel((packet, TransferEndPoint));

        var result = await RunAsync(channel, Context());

        Diagnostics.Assert("exit code", CurlExitCode.TftpNotFound, result.ExitCode);
        Assert.AreEqual(CurlExitCode.TftpNotFound, result.ExitCode);
        Assert.AreEqual("TFTP: File Not Found", result.ErrorMessage);
        Assert.HasCount(1, channel.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_OptionAcknowledgementWithAnOptionNameThatIsNotUtf8_IgnoresItAndKeeps512()
    {
        var channel = Channel(
            ([0, 6, 0xFF, 0xFE, 0, (byte)'1', 0], TransferEndPoint),
            Data(1, Payload(512, 'a')),
            Data(2, Payload(3, 'b')));
        var output = new MemoryStream();

        var result = await RunAsync(channel, Context(output));

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(515, output.Length);
        CollectionAssert.AreEqual(new ushort[] { 0, 1, 2 }, AcknowledgedBlocks(channel));
    }

    [TestMethod]
    public async Task ExecuteAsync_OptionAcknowledgementNamingBlksizeTwice_TakesTheLastOne()
    {
        var channel = Channel(
            OptionAcknowledgement("blksize\0256\0blksize\0128\0"),
            Data(1, Payload(128, 'a')),
            Data(2, Payload(5, 'b')));
        var output = new MemoryStream();

        var result = await RunAsync(channel, Context(output));

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(133, output.Length);
        CollectionAssert.AreEqual(new ushort[] { 0, 1, 2 }, AcknowledgedBlocks(channel));
    }

    [TestMethod]
    [DataRow("blksize\0+512\0", DisplayName = "plus sign")]
    [DataRow("blksize\0-8\0", DisplayName = "minus sign")]
    [DataRow("blksize\0 512\0", DisplayName = "leading space")]
    [DataRow("blksize\099999999999999999999\0", DisplayName = "too large for a long")]
    public async Task ExecuteAsync_OptionAcknowledgementBlksizeNotStartingWithADigitOrOverflowing_Exits71LargerThanMax(string options)
    {
        var channel = Channel(OptionAcknowledgement(options));

        var result = await RunAsync(channel, Context());

        Diagnostics.Assert("exit code", CurlExitCode.TftpIllegal, result.ExitCode);
        Assert.AreEqual(CurlExitCode.TftpIllegal, result.ExitCode);
        Assert.AreEqual("blksize is larger than max supported (65464)", result.ErrorMessage);
        Assert.HasCount(1, channel.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_FirstReplyIsAnEmptyBlock1_AcksItAndEndsWritingNothing()
    {
        var channel = Channel(Data(1, []));
        var output = new MemoryStream();

        var result = await RunAsync(channel, Context(output));

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0, output.Length);
        CollectionAssert.AreEqual(new ushort[] { 1 }, AcknowledgedBlocks(channel));
    }

    [TestMethod]
    public async Task ExecuteAsync_FirstReplyIsAShortBlock0_AcksBlock0AsARepeatAndEndsWritingNothing()
    {
        var channel = Channel(Data(0, Payload(3, 'x')));
        var output = new MemoryStream();

        var result = await RunAsync(channel, Context(output));

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0, output.Length);
        CollectionAssert.AreEqual(new ushort[] { 0 }, AcknowledgedBlocks(channel));
    }

    [TestMethod]
    [DataRow((ushort)65535, DisplayName = "block 65535, the one before 0")]
    [DataRow((ushort)3, DisplayName = "a block ahead")]
    [DataRow((ushort)32768, DisplayName = "half way round")]
    public async Task ExecuteAsync_FirstReplyIsABlockNeitherExpectedNorRepeated_IgnoresItAndTakesBlock1(ushort block)
    {
        var channel = Channel(Data(block, Payload(2, 'x')), Data(1, Payload(5, 'a')));
        var output = new MemoryStream();

        var result = await RunAsync(channel, Context(output));

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("aaaaa", Encoding.ASCII.GetString(output.ToArray()));
        CollectionAssert.AreEqual(new ushort[] { 1 }, AcknowledgedBlocks(channel));
    }

    [TestMethod]
    public async Task ExecuteAsync_SeededDuplicateAndOutOfOrderBlocks_WritesTheFileOnceInOrder()
    {
        const int seed = 1520;
        Diagnostics.Arrange("seed", seed);
        var random = new Random(seed);
        var blocks = new List<byte[]>();
        for (var index = 0; index < 40; index++)
        {
            var payload = new byte[index == 39 ? 100 : 512];
            random.NextBytes(payload);
            blocks.Add(payload);
        }

        var script = new List<(byte[] Datagram, EndPoint Source)>();
        for (var index = 0; index < blocks.Count; index++)
        {
            var block = (ushort)(index + 1);
            for (var noise = random.Next(0, 4); noise > 0 && index > 0; noise--)
            {
                var ahead = (ushort)(block + random.Next(1, 5));
                script.Add(random.Next(2) == 0
                    ? Data((ushort)(block - 1), blocks[index - 1])
                    : Data(ahead, new byte[512]));
            }

            script.Add(Data(block, blocks[index]));
        }

        var channel = new ScriptedDatagramChannel(ServerEndPoint, [.. script]);
        var output = new MemoryStream();

        var result = await new TftpProtocolHandler(Connector(channel)).ExecuteAsync(Context(output));

        TftpTestDiagnostics.Result(Diagnostics, result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(blocks.SelectMany(block => block).ToArray(), output.ToArray());
        Assert.AreEqual((ushort)40, AcknowledgedBlocks(channel)[^1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_SameHandlerTwiceInARow_SecondTransferStartsFresh()
    {
        var first = Channel(Data(1, Payload(512, 'a')), Data(2, Payload(1, 'a')));
        var second = Channel(Data(1, Payload(4, 'b')));
        var handler = new TftpProtocolHandler(new QueuedDatagramConnector([first, second]));
        var firstOutput = new MemoryStream();
        var secondOutput = new MemoryStream();

        var firstResult = await handler.ExecuteAsync(Context(firstOutput));
        var secondResult = await handler.ExecuteAsync(Context(secondOutput));

        TftpTestDiagnostics.Result(Diagnostics, secondResult);
        Assert.AreEqual(CurlExitCode.Ok, firstResult.ExitCode);
        Diagnostics.Assert("second exit code", CurlExitCode.Ok, secondResult.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, secondResult.ExitCode);
        Assert.AreEqual(513, firstOutput.Length);
        Assert.AreEqual("bbbb", Encoding.ASCII.GetString(secondOutput.ToArray()));
        CollectionAssert.AreEqual(new ushort[] { 1 }, AcknowledgedBlocks(second));
    }

    [TestMethod]
    public async Task ExecuteAsync_SameHandlerOnSixteenTransfersAtOnce_EachWritesItsOwnFile()
    {
        const int transfers = 16;
        var channels = Enumerable.Range(0, transfers)
            .Select(index => new ScriptedDatagramChannel(
                ServerEndPoint,
                Data(1, Payload(512, (char)('a' + index))),
                Data(2, Payload(index, (char)('a' + index)))))
            .ToArray();
        var handler = new TftpProtocolHandler(new QueuedDatagramConnector(channels));
        var outputs = Enumerable.Range(0, transfers).Select(_ => new MemoryStream()).ToArray();
        Diagnostics.Arrange("transfers", transfers);

        var results = await Task.WhenAll(outputs.Select(output => Task.Run(async () => await handler.ExecuteAsync(Context(output)))));

        Diagnostics.Act("exit codes", string.Join(",", results.Select(result => result.ExitCode)));
        Assert.IsTrue(results.All(result => result.ExitCode == CurlExitCode.Ok));
        var written = outputs
            .Select(output => Encoding.ASCII.GetString(output.ToArray()))
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToArray();
        var expected = Enumerable.Range(0, transfers)
            .Select(index => Payload(512 + index, (char)('a' + index)))
            .ToArray();
        CollectionAssert.AreEqual(expected, written);
    }

    [TestMethod]
    public async Task ExecuteAsync_TokenCancelledBeforeTheCall_ThrowsOperationCanceled()
    {
        var clock = new ManualTimeProvider();
        var channel = new FallsSilentDatagramChannel(ServerEndPoint, clock);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("tftp://h/file.txt"),
            Output = new MemoryStream(),
            TimeProvider = clock,
            CancellationToken = cancellation.Token,
        };
        Diagnostics.Arrange("token", "cancelled before the call");

        var thrown = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await new TftpProtocolHandler(new RecordingDatagramConnector(DatagramOpenResult.Opened(channel))).ExecuteAsync(context));

        Diagnostics.Act("thrown", thrown.GetType().Name);
        Diagnostics.Assert("clock", TimeSpan.Zero, clock.Now);
        Assert.AreEqual(TimeSpan.Zero, clock.Now);
    }

    private static string Payload(int length, char fill) => new(fill, length);

    private static (byte[] Datagram, EndPoint Source) Data(ushort block, string payload) =>
        Data(block, Encoding.ASCII.GetBytes(payload));

    private static (byte[] Datagram, EndPoint Source) Data(ushort block, ReadOnlySpan<byte> payload) =>
        ([0, 3, (byte)(block >> 8), (byte)block, .. payload], TransferEndPoint);

    private static (byte[] Datagram, EndPoint Source) OptionAcknowledgement(string options) =>
        ([0, 6, .. Encoding.ASCII.GetBytes(options)], TransferEndPoint);

    private static RecordingDatagramConnector Connector(ScriptedDatagramChannel channel) =>
        new(DatagramOpenResult.Opened(channel));

    /// <summary>
    /// The block numbers of every acknowledgement sent, skipping the read request.
    /// </summary>
    private static ushort[] AcknowledgedBlocks(ScriptedDatagramChannel channel) =>
        [.. channel.Sent.Skip(1).Select(sent =>
        {
            CollectionAssert.AreEqual(new byte[] { 0, 4 }, sent.Datagram[..2]);
            return (ushort)((sent.Datagram[2] << 8) | sent.Datagram[3]);
        })];

    private async Task<TransferResult> RunAsync(ScriptedDatagramChannel channel, TransferContext context)
    {
        TransferResult result;
        using (Diagnostics.Phase("execute"))
        {
            result = await new TftpProtocolHandler(Connector(channel)).ExecuteAsync(context);
        }

        TftpTestDiagnostics.Result(Diagnostics, result);
        TftpTestDiagnostics.Sent(Diagnostics, channel);
        return result;
    }

    private TransferContext Context(Stream? output = null, int? tftpBlockSize = null)
    {
        Diagnostics.Arrange("TFTP block size", tftpBlockSize?.ToString(CultureInfo.InvariantCulture) ?? "(default)");
        return new() { Url = CurlUrl.Parse("tftp://h/file.txt"), Output = output ?? new MemoryStream(), TftpBlockSize = tftpBlockSize };
    }

    private ScriptedDatagramChannel Channel(params (byte[] Datagram, EndPoint Source)[] script)
    {
        for (var index = 0; index < script.Length; index++)
        {
            TftpTestDiagnostics.Scripted(Diagnostics, index, script[index].Datagram, script[index].Source);
        }

        return new(ServerEndPoint, script);
    }
}
