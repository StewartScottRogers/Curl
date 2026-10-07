using System.Globalization;
using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Tftp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Tftp;

/// <summary>
/// Pins how a <c>tftp://</c> upload (<c>-T</c>) takes an ACK of block 65535, from curl
/// 8.21.0's <c>tftp_tx</c> (<c>lib/tftp.c</c> lines 371-385 at tag <c>curl-8_21_0</c>):
/// while block 0 is awaited - the write request's own ACK, or the ACK after the block
/// number wraps from 65535 to 0 - an ACK of 65535 counts as the awaited one, to work round
/// a tftpd-hpa bug; while any other block is awaited it is an ACK of the wrong block.
/// Pinned from the source because <c>Record-CurlExchange.ps1 -Tftp</c> cannot send a
/// chosen ACK.
/// </summary>
[TestClass]
public sealed class TftpUploadBlockWrapTests
{
    private const string UnexpectedAck65535Prefix = "Received ACK for block 65535, expecting";

    /// <summary>How many scripted and sent datagrams are written at each end of a long exchange.</summary>
    private const int LoggedAtEachEnd = 3;

    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 69);

    /// <summary>The server's transfer identifier: the new port it answers from.</summary>
    private static readonly IPEndPoint TransferEndPoint = new(IPAddress.Loopback, 50123);

    private readonly ManualTimeProvider clock = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_WriteRequestAnsweredWithAck65535_SendsData1WithoutReportingItAndCompletes()
    {
        var events = new RecordingTransferEvents();
        var channel = Channel(Ack(ushort.MaxValue), Ack(1));

        var result = await Run(channel, events, "abc"u8.ToArray());

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("bytes transferred", 3L, result.BytesTransferred);
        Assert.AreEqual(3, result.BytesTransferred);
        Diagnostics.Assert("datagrams sent", 2, channel.Sent.Count);
        Assert.HasCount(2, channel.Sent);
        Diagnostics.Diff("second datagram sent", new byte[] { 0, 3, 0, 1, (byte)'a', (byte)'b', (byte)'c' }, channel.Sent[1].Datagram);
        CollectionAssert.AreEqual(new byte[] { 0, 3, 0, 1, (byte)'a', (byte)'b', (byte)'c' }, channel.Sent[1].Datagram);
        Diagnostics.Assert("second datagram destination", TransferEndPoint, channel.Sent[1].Destination);
        Assert.AreEqual(TransferEndPoint, channel.Sent[1].Destination);
        Diagnostics.Assert("unexpected ACK 65535 reported", false, events.Steps.Any(step => step.StartsWith(UnexpectedAck65535Prefix, StringComparison.Ordinal)));
        Assert.IsFalse(events.Steps.Any(step => step.StartsWith(UnexpectedAck65535Prefix, StringComparison.Ordinal)));
    }

    /// <summary>
    /// No seam starts the block counter near the wrap, so the upload really runs 65537
    /// blocks: an OACK grants the smallest block size, 8, and 65536 full blocks plus 3 bytes
    /// are sent as blocks 1 to 65535, 0 and 1. The ACK awaited for block 0 comes as 65535.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_AckAfterTheBlockNumberWrapsTo0ComesAs65535_SendsTheNextBlockWithoutAResend()
    {
        const int BlockSize = 8;
        var upload = new byte[(65536 * BlockSize) + 3];
        upload.AsSpan(upload.Length - 3).Fill((byte)'z');
        List<(byte[] Datagram, EndPoint Source)> script = [OptionAcknowledgement("blksize\08\0")];
        script.AddRange(Enumerable.Range(1, ushort.MaxValue).Select(block => Ack((ushort)block)));
        script.Add(Ack(ushort.MaxValue));
        script.Add(Ack(1));
        var events = new RecordingTransferEvents();
        var channel = Channel([.. script]);

        var result = await Run(channel, events, upload);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("bytes transferred", (long)upload.Length, result.BytesTransferred);
        Assert.AreEqual(upload.Length, result.BytesTransferred);
        var blocks = DataBlocksSent(channel);
        Diagnostics.Assert("DATA blocks sent", 65537, blocks.Length);
        Assert.HasCount(65537, blocks);
        Diagnostics.Assert("last three DATA blocks", "65535 0 1", string.Join(" ", blocks[^3..]));
        CollectionAssert.AreEqual(new ushort[] { ushort.MaxValue, 0, 1 }, blocks[^3..]);
        Diagnostics.Diff("last datagram sent", new byte[] { 0, 3, 0, 1, (byte)'z', (byte)'z', (byte)'z' }, channel.Sent[^1].Datagram);
        CollectionAssert.AreEqual(new byte[] { 0, 3, 0, 1, (byte)'z', (byte)'z', (byte)'z' }, channel.Sent[^1].Datagram);
        Diagnostics.Assert("unexpected ACK 65535 reported", false, events.Steps.Any(step => step.StartsWith(UnexpectedAck65535Prefix, StringComparison.Ordinal)));
        Assert.IsFalse(events.Steps.Any(step => step.StartsWith(UnexpectedAck65535Prefix, StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_Ack65535WhileBlock1IsAwaited_ReportsItAsUnexpectedAndResendsData1()
    {
        var events = new RecordingTransferEvents();
        var channel = Channel(Ack(0), Ack(ushort.MaxValue), Ack(1));

        var result = await Run(channel, events, "abc"u8.ToArray());

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("DATA blocks sent", "1 1", string.Join(" ", DataBlocksSent(channel)));
        CollectionAssert.AreEqual(new ushort[] { 1, 1 }, DataBlocksSent(channel));
        Diagnostics.Diff("resent DATA", channel.Sent[1].Datagram, channel.Sent[2].Datagram);
        CollectionAssert.AreEqual(channel.Sent[1].Datagram, channel.Sent[2].Datagram);
        Diagnostics.Act("events", string.Join(" | ", events.Steps));
        Diagnostics.Assert(
            "unexpected ACK 65535 reports",
            1,
            events.Steps.Count(step => step == "Received ACK for block 65535, expecting 1"));
        Assert.AreEqual(1, events.Steps.Count(step => step == "Received ACK for block 65535, expecting 1"));
    }

    private static (byte[] Datagram, EndPoint Source) Ack(ushort block) =>
        ([0, 4, (byte)(block >> 8), (byte)block], TransferEndPoint);

    private static (byte[] Datagram, EndPoint Source) OptionAcknowledgement(string options) =>
        ([0, 6, .. System.Text.Encoding.ASCII.GetBytes(options)], TransferEndPoint);

    /// <summary>
    /// The block numbers of every DATA packet sent, in order.
    /// </summary>
    private static ushort[] DataBlocksSent(FallsSilentDatagramChannel channel) =>
        [.. channel.Sent
            .Where(sent => sent.Datagram[1] == 3)
            .Select(sent => (ushort)((sent.Datagram[2] << 8) | sent.Datagram[3]))];

    private async Task<TransferResult> Run(FallsSilentDatagramChannel channel, ITransferEvents events, byte[] upload)
    {
        Diagnostics.Arrange("url", "tftp://h/dest.txt");
        Diagnostics.Arrange("upload length", upload.Length);
        Diagnostics.Arrange("manual clock start", clock.Now);
        try
        {
            TransferResult result;
            using (Diagnostics.Phase("upload"))
            {
                result = await new TftpProtocolHandler(new RecordingDatagramConnector(DatagramOpenResult.Opened(channel)))
                    .ExecuteAsync(new TransferContext
                    {
                        Url = CurlUrl.Parse("tftp://h/dest.txt"),
                        Output = new MemoryStream(),
                        Upload = new MemoryStream(upload),
                        Events = events,
                        TimeProvider = clock,
                    });
            }

            TftpTestDiagnostics.Result(Diagnostics, result);
            return result;
        }
        finally
        {
            LogSent(channel);
        }
    }

    /// <summary>
    /// Writes the count of datagrams sent and, as BYTES lines, only the first and last few:
    /// a wrap test sends tens of thousands.
    /// </summary>
    private void LogSent(FallsSilentDatagramChannel channel)
    {
        Diagnostics.Act("datagrams sent", channel.Sent.Count);
        for (var index = 0; index < channel.Sent.Count; index++)
        {
            if (index >= LoggedAtEachEnd && index < channel.Sent.Count - LoggedAtEachEnd)
            {
                continue;
            }

            var (datagram, destination, at) = channel.Sent[index];
            TftpTestDiagnostics.Datagram(
                Diagnostics,
                string.Create(CultureInfo.InvariantCulture, $"sent {index} to {destination} at {at}"),
                datagram);
        }
    }

    private FallsSilentDatagramChannel Channel(params (byte[] Datagram, EndPoint Source)[] script)
    {
        Diagnostics.Arrange("scripted datagrams", script.Length);
        for (var index = 0; index < script.Length; index++)
        {
            if (index >= LoggedAtEachEnd && index < script.Length - LoggedAtEachEnd)
            {
                continue;
            }

            TftpTestDiagnostics.Scripted(Diagnostics, index, script[index].Datagram, script[index].Source);
        }

        return new(ServerEndPoint, clock, script);
    }
}
