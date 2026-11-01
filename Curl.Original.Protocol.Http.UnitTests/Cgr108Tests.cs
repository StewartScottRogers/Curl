using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-108 (BL-3637): curl 8.22.0 parses the first response line before it refuses a
/// carriage return in it, so a bare CR in a line that is no status line fails as HTTP/0.9 or an
/// unsupported subversion (exit 1), and only a line that parses as a status line fails as
/// <c>Carriage return found in header</c> (exit 8), as does a CR in a header line (measured).
/// </summary>
[TestClass]
public sealed class Cgr108Tests
{
    [TestMethod]
    [DataRow("abc\rdef\n", CurlExitCode.UnsupportedProtocol, "Received HTTP/0.9 when not allowed", DisplayName = "No status line")]
    [DataRow("HTTP\r/1.1 200 OK\r\n\r\n", CurlExitCode.UnsupportedProtocol, "Received HTTP/0.9 when not allowed", DisplayName = "CR inside HTTP/")]
    [DataRow("HTTP/1.1 2\r00 OK\r\n\r\n", CurlExitCode.UnsupportedProtocol, "Unsupported HTTP/1 subversion in response", DisplayName = "CR inside the status code")]
    [DataRow("HTTP/1.1 200 O\rK\r\nContent-Length: 0\r\n\r\n", CurlExitCode.WeirdServerReply, "Carriage return found in header", DisplayName = "CR inside the reason phrase")]
    [DataRow("HTTP/1.0 200 OK\r\r\nContent-Length: 0\r\n\r\n", CurlExitCode.WeirdServerReply, "Carriage return found in header", DisplayName = "CR before CRLF")]
    [DataRow("HTTP/1.1 200 OK\r\nA: b\rc\r\nContent-Length: 0\r\n\r\n", CurlExitCode.WeirdServerReply, "Carriage return found in header", DisplayName = "CR inside a header")]
    public async Task ReadAsync_BareCarriageReturnInTheHead_FailsAsCurlDoes(string response, CurlExitCode exitCode, string message)
    {
        ScriptedConnection connection = new(Encoding.Latin1.GetBytes(response), 1);

        HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None));

        Assert.AreEqual(exitCode, thrown.ExitCode);
        Assert.AreEqual(message, thrown.Message);
    }
}
