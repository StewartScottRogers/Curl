namespace Curl.Original.Protocol.Http;

/// <summary>
/// Third-party gap report issue CGR-236 (BL-3765): a header holding both a NUL and a carriage
/// return failed as <c>Carriage return found in header</c>; curl 8.22.0 checks the NUL first
/// and fails with exit 8, <c>Nul byte in header</c>, in either order.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nX: a\0b\rc\r\n\r\n", DisplayName = "NUL before the carriage return")]
    [DataRow("HTTP/1.1 200 OK\r\nX: a\rb\0c\r\n\r\n", DisplayName = "carriage return before the NUL")]
    public async Task ExecuteAsync_HeaderWithNulAndCarriageReturn_FailsWithNulByteInHeader(string response)
    {
        await AssertWeirdHeaderLineAsync(response, "", "Nul byte in header", ["< HTTP/1.1 200 OK\r\n"]);
    }
}
