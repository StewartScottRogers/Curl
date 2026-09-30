using System.Text;

using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="ConnectReplyHeadWritingEvents" />: a CONNECT reply head goes to the header
/// output byte for byte, and every other event goes on to the transfer's own events unchanged.
/// </summary>
[TestClass]
public sealed class ConnectReplyHeadWritingEventsTests
{
    [TestMethod]
    public async Task WriteConnectReplyHeadAsync_WritesTheHeadToTheHeaderOutput()
    {
        using MemoryStream headerOutput = new();
        CallRecordingEvents inner = new();
        ConnectReplyHeadWritingEvents events = new(inner, headerOutput);

        await events.WriteConnectReplyHeadAsync(Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\n\r\n"), CancellationToken.None);

        Assert.AreEqual("HTTP/1.1 200 OK\r\n\r\n", Encoding.Latin1.GetString(headerOutput.ToArray()));
        Assert.IsEmpty(inner.Calls);
    }

    [TestMethod]
    public void EveryOtherEvent_GoesOnToTheInnerEvents()
    {
        using MemoryStream headerOutput = new();
        CallRecordingEvents inner = new();
        ITransferEvents events = new ConnectReplyHeadWritingEvents(inner, headerOutput);

        events.ReportInfo("text");
        events.ReportConnectionOpened(null!);
        events.ReportConnectionReused(null!);
        events.ReportTlsHandshake(null!);
        events.ReportTlsData([1], sent: true);
        events.ReportTlsMessage(null!);
        events.ReportTlsTrust(null!);
        events.ReportRequestHeader([2]);
        events.ReportResponseHeader([3]);
        events.ReportDataSent([4]);
        events.ReportDataReceived([5]);

        CollectionAssert.AreEqual(
            new[]
            {
                "Info text", "Opened", "Reused", "Handshake", "TlsData 1 True", "TlsMessage", "TlsTrust",
                "RequestHeader 2", "ResponseHeader 3", "DataSent 4", "DataReceived 5",
            },
            inner.Calls);
        Assert.AreEqual(0, headerOutput.Length);
    }

    /// <summary>Records each event it is given as one line naming it and its payload.</summary>
    private sealed class CallRecordingEvents : ITransferEvents
    {
        public List<string> Calls { get; } = [];

        public void ReportInfo(string text) => Calls.Add($"Info {text}");

        public void ReportConnectionOpened(ConnectionOpenedEvent opened) => Calls.Add("Opened");

        public void ReportConnectionReused(ConnectionReusedEvent reused) => Calls.Add("Reused");

        public void ReportTlsHandshake(TlsHandshakeEvent handshake) => Calls.Add("Handshake");

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => Calls.Add($"TlsData {bytes[0]} {sent}");

        public void ReportTlsMessage(TlsMessageEvent message) => Calls.Add("TlsMessage");

        public void ReportTlsTrust(TlsTrustEvent trust) => Calls.Add("TlsTrust");

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Calls.Add($"RequestHeader {bytes[0]}");

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Calls.Add($"ResponseHeader {bytes[0]}");

        public void ReportDataSent(ReadOnlySpan<byte> bytes) => Calls.Add($"DataSent {bytes[0]}");

        public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Calls.Add($"DataReceived {bytes[0]}");
    }
}
