using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Engine;
using Curl.Original.Protocol.Http;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins report 2's <c>--raw</c> chunked repro end to end through the runner and
/// <see cref="HttpProtocolHandler" /> over a <see cref="StallingConnector" />, whose server sends the
/// chunked response and then goes silent without closing: curl 8.22.0 writes the chunk framing
/// through the zero-length chunk and its trailers and ends there, exit 0 (BL-3162 Notes), so the run
/// must end without waiting for a close (BL-3192, ADR-0780).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerRawTests
{
    private const string ChunkedBody = "6\r\nhello \r\n5\r\nworld\r\n0\r\nX-T: v\r\n\r\n";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_RawOverChunkedOnAConnectionLeftOpen_WritesTheChunkFramingAndEnds()
    {
        StallingConnector server = new(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n" + ChunkedBody));
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));
        MemoryStream standardOutput = new();
        MemoryStream standardError = new();
        InMemoryFileSystem files = new();
        Diagnostics.Arrange("arguments", "-sS --raw http://127.0.0.1:18180/a");

        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http])),
                files,
                files,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                writesProgressMeter: false)
            .RunAsync(["-sS", "--raw", "http://127.0.0.1:18180/a"])
            .WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout bytes", standardOutput.ToArray());
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(ChunkedBody, Encoding.ASCII.GetString(standardOutput.ToArray()));
        Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(standardError.ToArray()));
    }
}
