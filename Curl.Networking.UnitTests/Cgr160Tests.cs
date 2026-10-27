using System.Text;

using Curl.Networking.Fakes;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// CGR-160 (BL-3689): a DoH response framed by neither <c>Content-Length</c> nor chunked coding is
/// delimited by the close, and curl 8.22.0 reads it to the end, decodes it and resolves the name,
/// for an HTTP/1.0 and an HTTP/1.1 answer alike (measured); a body that runs past curl's 3000-byte
/// DoH buffer is still a receive failure.
/// </summary>
[TestClass]
public sealed class Cgr160Tests
{
    private static readonly byte[] Answer = [0x00, 0x00, 0x81, 0x80, 0x00, 0x01, 0x00, 0x01, 0x0D, 0x0A];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("HTTP/1.0 200 OK")]
    [DataRow("HTTP/1.1 200 OK")]
    public async Task ReadBodyAsync_WithACloseDelimitedBody_ReturnsTheBytesUpToTheClose(string statusLine)
    {
        Diagnostics.Arrange("status line", statusLine);

        var body = await ReadAsync(statusLine, Answer);

        Diagnostics.Diff("body", Answer, body);
        CollectionAssert.AreEqual(Answer, body);
    }

    [TestMethod]
    public async Task ReadBodyAsync_WithACloseDelimitedBodyOfTheMaximumLength_ReturnsIt()
    {
        var body = await ReadAsync("HTTP/1.0 200 OK", new byte[DohResponseReader.MaximumBodyLength]);

        Diagnostics.Assert("body length", DohResponseReader.MaximumBodyLength, body?.Length);
        Assert.HasCount(DohResponseReader.MaximumBodyLength, body!);
    }

    [TestMethod]
    public async Task ReadBodyAsync_WithACloseDelimitedBodyOverCurlsBuffer_ReturnsNull()
    {
        var body = await ReadAsync("HTTP/1.0 200 OK", new byte[DohResponseReader.MaximumBodyLength + 1]);

        Diagnostics.Assert("body", "null", body is null ? "null" : $"{body.Length} bytes");
        Assert.IsNull(body);
    }

    private async Task<byte[]?> ReadAsync(string statusLine, byte[] body)
    {
        byte[] response = [.. Encoding.Latin1.GetBytes(statusLine + "\r\nContent-Type: application/dns-message\r\n\r\n"), .. body];
        Diagnostics.Bytes("response", response);
        var connection = new ScriptedConnection(response);

        var read = await DohResponseReader.ReadBodyAsync(connection, CancellationToken.None);

        Diagnostics.Act("body", read is null ? "null" : $"{read.Length} bytes");
        return read;
    }
}
