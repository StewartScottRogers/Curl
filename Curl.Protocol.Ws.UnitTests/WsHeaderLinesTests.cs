using System.Text;

namespace Curl.Protocol.Ws;

/// <summary>Pins the header line checks curl 8.21.0 makes on every upgrade reply head (BL-1405).</summary>
[TestClass]
public sealed class WsHeaderLinesTests
{
    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nA: b\r\n\r\n")]
    [DataRow("HTTP/1.1 200 OK\nA: b\n\n")]
    [DataRow("HTTP/1.1 200 OK\r\nA: b\r\n continued\r\n\tagain\r\n\r\n")]
    [DataRow("HTTP/1.1 302 Found\r\nLocation:\r\nLocation: /x\r\nLOCATION: /x\r\nLocation: \r\n\r\n")]
    [DataRow("HTTP/1.1 302 Found\r\nLocations: /x\r\nLocation: /y\r\n\r\n")]
    public void FindRefusedLine_AcceptedHead_ReturnsTheHeadLength(string head)
    {
        byte[] bytes = Encoding.Latin1.GetBytes(head);

        int lineStart = WsHeaderLines.FindRefusedLine(bytes, out string? refusal);

        Assert.AreEqual(bytes.Length, lineStart);
        Assert.IsNull(refusal);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\n continued\r\n\r\n", WsHeaderLines.HeaderWithoutColon)]
    [DataRow("HTTP/1.1 200 OK\nBad\n\n", WsHeaderLines.HeaderWithoutColon)]
    [DataRow("HTTP/1.1 200 OK\r\n a\0\r\n\r\n", WsHeaderLines.NulByteInHeader)]
    [DataRow("HTTP/1.1 200 OK\r\n\ra: b\r\n\r\n", WsHeaderLines.CarriageReturnInHeader)]
    [DataRow("HTTP/1.1 200 OK\r\nA: \0\r\r\n\r\n", WsHeaderLines.CarriageReturnInHeader)]
    [DataRow("HTTP/1.1 302 Found\r\nLocation: /x\r\nLocation: /X\r\n\r\n", WsHeaderLines.MultipleLocationHeaders)]
    public void FindRefusedLine_RefusedLine_ReturnsWhereItStartsAndTheMessage(string head, string message)
    {
        byte[] bytes = Encoding.Latin1.GetBytes(head);
        int expectedStart = head.IndexOf('\n', StringComparison.Ordinal) + 1;
        if (message == WsHeaderLines.MultipleLocationHeaders)
        {
            expectedStart = head.LastIndexOf("Location", StringComparison.Ordinal);
        }

        int lineStart = WsHeaderLines.FindRefusedLine(bytes, out string? refusal);

        Assert.AreEqual(expectedStart, lineStart);
        Assert.AreEqual(message, refusal);
    }
}
