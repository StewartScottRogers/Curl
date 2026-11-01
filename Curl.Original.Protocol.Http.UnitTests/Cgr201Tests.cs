using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

/// <summary>CGR-201: an <c>HTTP/2 200 OK</c> status line over HTTP/1.1 where curl is built without HTTP/2.</summary>
[TestClass]
public sealed class Cgr201Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void RefusalOverHttp1_Http2WithoutHttp2Build_IsUnsupportedVersionExit1()
    {
        Diagnostics.Arrange("line", "HTTP/2 200 OK");

        HttpTransferException refusal = HttpStatusLine.RefusalOverHttp1("HTTP/2 200 OK", withoutHttp2: true);

        Diagnostics.Assert("exit code", CurlExitCode.UnsupportedProtocol, refusal.ExitCode);
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, refusal.ExitCode);
        Assert.AreEqual("Unsupported HTTP version (2.0) in response", refusal.Message);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ReadAsync_Http2200OverHttp1OnWindows_IsUnsupportedVersionExit1()
    {
        ScriptedConnection connection = new(Encoding.Latin1.GetBytes("HTTP/2 200 OK\r\nContent-Length: 2\r\n\r\nok"), 65536);

        HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None));

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, thrown.ExitCode);
        Assert.AreEqual("Unsupported HTTP version (2.0) in response", thrown.Message);
    }
}
