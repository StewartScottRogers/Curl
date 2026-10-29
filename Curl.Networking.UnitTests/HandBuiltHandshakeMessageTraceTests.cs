using System.Security.Authentication;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using Curl.Tls;

namespace Curl.Networking;

[TestClass]
public sealed class HandBuiltHandshakeMessageTraceTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow((int)SslProtocols.Tls13, 0x0304)]
    [DataRow((int)SslProtocols.Tls12, 0x0303)]
    [DataRow(768, 0x0302)]
    [DataRow(192, 0x0301)]
    [DataRow((int)SslProtocols.None, 0x0305)]
    public void ReportHeld_AfterTheServerAnswered_ReportsTheHelloAsOfferedAndTheRestAsNegotiated(int negotiated, int expectedVersion)
    {
        var events = new RecordingTransferEvents();
        var trace = new HandBuiltHandshakeMessageTrace(events, 0x0305);
        Diagnostics.Arrange("negotiated", negotiated);

        trace.Observe(TlsTrafficDirection.Write, Tls.TlsContentType.Handshake, [1, 0, 0, 0]);
        var heldBeforeTheEnd = events.TlsMessages.Count;
        trace.Observe(TlsTrafficDirection.Read, Tls.TlsContentType.Handshake, [2, 0, 0, 0]);
        trace.Observe(TlsTrafficDirection.Write, Tls.TlsContentType.ChangeCipherSpec, [1]);
        trace.ReportHeld((SslProtocols)negotiated);
        trace.Observe(TlsTrafficDirection.Read, Tls.TlsContentType.Handshake, [4, 0, 0, 0]);
        var reported = events.TlsMessages.Select(message => $"{message.Sent} {message.ProtocolVersion:x4} {message.ContentType} {Convert.ToHexString(message.Bytes.Span)}").ToArray();
        Diagnostics.Act("reported", string.Join(" | ", reported));

        Assert.AreEqual(0, heldBeforeTheEnd);
        var version = expectedVersion.ToString("x4", System.Globalization.CultureInfo.InvariantCulture);
        string[] expected =
        [
            "True 0305 Handshake 01000000",
            $"False {version} Handshake 02000000",
            $"True {version} ChangeCipherSpec 01",
            $"False {version} Handshake 04000000",
        ];
        CollectionAssert.AreEqual(expected, reported);
    }
}
