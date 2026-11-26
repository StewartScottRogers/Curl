using System.Text;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins BL-5955 on <see cref="HttpProtocolHandler" />: <c>-I</c> answered with a
/// <c>101 Switching Protocols</c> that no h2c upgrade asked for ends with exit 52,
/// <c>Empty reply from server</c>, after one read past the head - the bytes that came with it,
/// or else the next bytes or the close - and the connection is shut down, as curl 8.22.0 does.
/// </summary>
[TestClass]
public sealed class HeadUnsolicitedSwitchingProtocolsTests
{
    private const string Head = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: x\r\n\r\n";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(Head + "no\n", 65536, "no\n", DisplayName = "Bytes with the head")]
    [DataRow(Head, 65536, "", DisplayName = "Closed after the head")]
    [DataRow(Head + "abc", 1, "a", DisplayName = "Bytes after the head: one read")]
    [DataRow("HTTP/1.1 101 S\r\nContent-Length: 3\r\n\r\n", 65536, "", DisplayName = "Content-Length ignored")]
    [DataRow("HTTP/1.0 101 S\r\nConnection: keep-alive\r\n\r\n", 65536, "", DisplayName = "HTTP/1.0 keep-alive")]
    public async Task ExecuteAsync_HeadAnsweredWithUnsolicitedSwitchingProtocols_FailsWithEmptyReplyAndShutsTheConnectionDown(string response, int readSize, string data)
    {
        MemoryStream output = new();
        MemoryStream headerOutput = new();
        RecordingTransferEvents events = new();
        ScriptedConnection connection = new(Encoding.Latin1.GetBytes(response), readSize);
        Diagnostics.Arrange("response, -I", response);

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator())
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse("http://127.0.0.1:18231/"),
                Output = output,
                HeaderOutput = headerOutput,
                Events = events,
                NoBody = true,
                Http = new HttpRequestOptions { UserAgent = "curl/8.22.0" },
            });

        string[] ending = [.. events.Events.TakeLast(3)];
        Diagnostics.Act("exit code, message, last lines", string.Join(" | ", [result.ExitCode.ToString(), result.ErrorMessage ?? "", .. ending]));
        Diagnostics.Assert("exit code", CurlExitCode.GotNothing, result.ExitCode);
        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        Assert.AreEqual("Empty reply from server", result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "{ " + data, "* Empty reply from server", "* shutting down connection #0" }, ending);
        Assert.AreEqual(101, result.Report?.ResponseCode);
        Assert.AreEqual(0, result.Report?.DownloadSize);
        Assert.AreEqual(0, output.Length);
        Assert.AreEqual(response.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4, headerOutput.Length);
        Assert.IsTrue(connection.IsDisposed);
    }
}
