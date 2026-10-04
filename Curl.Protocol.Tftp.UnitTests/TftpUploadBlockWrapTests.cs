using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Tftp.Fakes;

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

    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 69);

    /// <summary>The server's transfer identifier: the new port it answers from.</summary>
    private static readonly IPEndPoint TransferEndPoint = new(IPAddress.Loopback, 50123);

    private readonly ManualTimeProvider clock = new();

    [TestMethod]
    public async Task ExecuteAsync_WriteRequestAnsweredWithAck65535_SendsData1WithoutReportingItAndCompletes()
    {
        var events = new RecordingTransferEvents();
        var channel = Channel(Ack(ushort.MaxValue), Ack(1));

        var result = await Run(channel, events, "abc"u8.ToArray());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(3, result.BytesTransferred);
        Assert.HasCount(2, channel.Sent);
        CollectionAssert.AreEqual(new byte[] { 0, 3, 0, 1, (byte)'a', (byte)'b', (byte)'c' }, channel.Sent[1].Datagram);
        Assert.AreEqual(TransferEndPoint, channel.Sent[1].Destination);
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

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(upload.Length, result.BytesTransferred);
        var blocks = DataBlocksSent(channel);
        Assert.HasCount(65537, blocks);
        CollectionAssert.AreEqual(new ushort[] { ushort.MaxValue, 0, 1 }, blocks[^3..]);
        CollectionAssert.AreEqual(new byte[] { 0, 3, 0, 1, (byte)'z', (byte)'z', (byte)'z' }, channel.Sent[^1].Datagram);
        Assert.IsFalse(events.Steps.Any(step => step.StartsWith(UnexpectedAck65535Prefix, StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_Ack65535WhileBlock1IsAwaited_ReportsItAsUnexpectedAndResendsData1()
    {
        var events = new RecordingTransferEvents();
        var channel = Channel(Ack(0), Ack(ushort.MaxValue), Ack(1));

        var result = await Run(channel, events, "abc"u8.ToArray());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new ushort[] { 1, 1 }, DataBlocksSent(channel));
        CollectionAssert.AreEqual(channel.Sent[1].Datagram, channel.Sent[2].Datagram);
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

    private async Task<TransferResult> Run(FallsSilentDatagramChannel channel, ITransferEvents events, byte[] upload) =>
        await new TftpProtocolHandler(new RecordingDatagramConnector(DatagramOpenResult.Opened(channel)))
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse("tftp://h/dest.txt"),
                Output = new MemoryStream(),
                Upload = new MemoryStream(upload),
                Events = events,
                TimeProvider = clock,
            });

    private FallsSilentDatagramChannel Channel(params (byte[] Datagram, EndPoint Source)[] script) =>
        new(ServerEndPoint, clock, script);
}
