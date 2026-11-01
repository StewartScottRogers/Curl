using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Third-party gap report issue CGR-121 (BL-3650): HTTP/2 against a peer that answers in
/// HTTP/1.1 fails with exit 16 and nghttp2's "expected SETTINGS frame" text, as upstream
/// test1801 expects and as curl 8.18.0 (nghttp2 1.68.0, WSL Ubuntu) gave in every run
/// against a server that keeps the connection open, measured on 2026-10-26 (BL-3650 Notes).
/// </summary>
[TestClass]
public sealed class Cgr121Tests
{
    private const string Http11Reply = "HTTP/1.1 200 OK\r\nContent-Length: 6\r\n\r\nlies!\n";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(HttpVersionPreference.Http2PriorKnowledge, "", DisplayName = "--http2-prior-knowledge")]
    [DataRow(HttpVersionPreference.Http2, "HTTP/1.1 101 Switching!\r\n\r\n", DisplayName = "--http2 upgrade, 101 then HTTP/1.1")]
    public async Task ExecuteAsync_PeerAnswersInHttp11_FailsWithExit16(HttpVersionPreference version, string switching)
    {
        string reply = switching + Http11Reply;
        ScriptedConnection connection = new(Encoding.Latin1.GetBytes(reply), 65536);
        Diagnostics.Arrange("version, reply", $"{version}, {reply.Replace("\r", "\\r").Replace("\n", "\\n")}");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator())
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse("http://127.0.0.1:21354/1801"),
                Output = new MemoryStream(),
                Http = new Curl.Protocol.Abstractions.HttpRequestOptions { Version = version },
            });

        Diagnostics.Act("exit code, message", $"{result.ExitCode}, {result.ErrorMessage}");
        Diagnostics.Assert("exit code", CurlExitCode.Http2, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Http2, result.ExitCode);
        Assert.AreEqual("Remote peer returned unexpected data while we expected SETTINGS frame.  Perhaps, peer does not support HTTP/2 properly.", result.ErrorMessage);
    }
}
