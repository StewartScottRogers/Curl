using System.Text;
using Curl.Extended.Protocol.Coap.Client;
using Curl.Extended.Protocol.Coap.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Coap.Observe;

/// <summary>Checks that an observation tells the transfer's progress reporter the running total of the bytes it wrote, so a <c>-m</c> timeout and <c>%{size_download}</c> count them.</summary>
[TestClass]
public sealed class CoapObservationProgressTests
{
    private static readonly byte[] Token = [0, 0, 0, 0];

    [TestMethod]
    public async Task ExecuteAsync_TwoNotifications_ReportsTheRunningTotalOfBytesWrittenAfterEach()
    {
        var channel = new ScriptedDatagramChannel((count, sent) => count == 1
            ? [Piggybacked(sent, 5, "v1"), Notification(6, "v2"), Notification(7, null, "end")]
            : []);
        var progress = new RecordingProgress();
        var output = new MemoryStream();

        TransferResult result = await Handler(channel).ExecuteAsync(Observed(output, progress, CancellationToken.None));

        CollectionAssert.AreEqual(new long[] { 3, 6, 10 }, progress.Totals);
        Assert.AreEqual(output.Length, progress.Totals[^1]);
        Assert.AreEqual(output.Length, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledAfterNotifications_HasReportedEveryByteOnTheOutput()
    {
        using var cancellation = new CancellationTokenSource();
        var channel = new ScriptedDatagramChannel((count, sent) =>
        {
            if (count == 2)
            {
                cancellation.Cancel();
            }

            return count == 1 ? [Piggybacked(sent, 5, "v1"), Notification(7, 6, "v2", CoapMessageType.Confirmable)] : [];
        });
        var progress = new RecordingProgress();
        var output = new MemoryStream();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await Handler(channel).ExecuteAsync(Observed(output, progress, cancellation.Token)));

        Assert.AreEqual("v1\n", Encoding.ASCII.GetString(output.ToArray()));
        Assert.AreEqual(output.Length, progress.Totals[^1]);
    }

    private static CoapProtocolHandler Handler(ScriptedDatagramChannel channel) =>
        new(new ScriptedDatagramConnector(DatagramOpenResult.Opened(channel)), length => new byte[length], null!);

    private static CoapObserveContext Observed(Stream output, ITransferProgress progress, CancellationToken cancellation) =>
        new(new TransferContext { Url = CurlUrl.Parse("coap://h/r"), Output = output, Progress = progress, CancellationToken = cancellation, TimeProvider = new SteppingTimeProvider(TimeSpan.Zero) });

    private static byte[] Piggybacked(CoapMessage request, uint sequence, string payload) =>
        CoapMessageEncoding.Encode(new CoapMessage(CoapMessageType.Acknowledgement, CoapCode.Content, request.MessageId, request.Token, [Observe(sequence)], Encoding.ASCII.GetBytes(payload)));

    private static byte[] Notification(ushort messageId, uint? sequence, string payload, CoapMessageType type = CoapMessageType.NonConfirmable) =>
        CoapMessageEncoding.Encode(new CoapMessage(type, CoapCode.Content, messageId, Token, sequence is { } value ? [Observe(value)] : [], Encoding.ASCII.GetBytes(payload)));

    private static byte[] Notification(ushort messageId, string payload) => Notification(messageId, (uint)messageId, payload);

    private static CoapOption Observe(uint sequence) => new(CoapOptionNumber.Observe, CoapObserveValue.Encode(sequence));

    private sealed class RecordingProgress : ITransferProgress
    {
        public List<long> Totals { get; } = [];

        public void ReportTransferStarted()
        {
        }

        public void ReportDownloaded(long bytesSoFar, long? expectedTotal) => Totals.Add(bytesSoFar);

        public void ReportUploaded(long bytesSoFar, long? expectedTotal)
        {
        }
    }
}
