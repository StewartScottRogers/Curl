using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// CGR-185 (BL-3714): a Linux <c>--vlan-priority</c> the system refuses (<c>SO_PRIORITY</c> without
/// <c>CAP_NET_ADMIN</c>) prints <c>Warning: VLAN priority 7 failed with errno 1: Operation not permitted</c>
/// and connects anyway, as curl does.
/// </summary>
[TestClass]
public sealed class Cgr185Tests
{
    [TestMethod]
    public void VlanPriorityFailure_NotPermitted_WordsTheWarningAsCurlDoes()
    {
        string warning = QualityOfServiceSocketOptions.VlanPriorityFailure(7, 1, "Operation not permitted");

        Assert.AreEqual("VLAN priority 7 failed with errno 1: Operation not permitted", warning);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TrySet_PriorityTheSystemRefuses_ReportsTheWarning()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var events = new RecordingEvents();

        QualityOfServiceSocketOptions.TrySet(socket, new RawSocketOption(1, 12, 7), events);

        Assert.AreEqual(1, events.Warnings.Count);
        StringAssert.StartsWith(events.Warnings[0], "VLAN priority 7 failed with errno ");
    }

    [TestMethod]
    public void TrySet_OtherOptionTheSystemRefuses_ReportsNoWarning()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var events = new RecordingEvents();

        QualityOfServiceSocketOptions.TrySet(socket, new RawSocketOption(0, 9999, 1), events);

        Assert.AreEqual(0, events.Warnings.Count);
    }

    private sealed class RecordingEvents : ITransferEvents
    {
        public List<string> Warnings { get; } = [];

        public void ReportWarning(string text) => Warnings.Add(text);

        public void ReportConnectionOpened(ConnectionOpenedEvent opened)
        {
        }

        public void ReportConnectionReused(ConnectionReusedEvent reused)
        {
        }

        public void ReportDataReceived(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportDataSent(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportInfo(string text)
        {
        }

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent)
        {
        }

        public void ReportTlsHandshake(TlsHandshakeEvent handshake)
        {
        }
    }
}
