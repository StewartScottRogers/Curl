using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Tests that a <c>-d</c> body under <c>--limit-rate</c> reaches the HTTP handler's connection no
/// faster than the rate, as curl 8.22.0's <c>CURLOPT_MAX_SEND_SPEED_LARGE</c> holds it (BL-2957).
/// </summary>
[TestClass]
public sealed class RateLimitedDataBodyTests
{
    private const string Body = "0123456789abcdefghijABCDEFGHIJ";

    private readonly ImmediateTimerTimeProvider clock = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_ThirtyByteDataBodyAtTenASecond_SendsItInTenByteSeconds()
    {
        ScriptedConnector server = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok")]);

        int exitCode = await RunAsync(server, "-d", Body, "--limit-rate", "10", "http://127.0.0.1:18241/a");

        string written = Encoding.Latin1.GetString(server.Written);
        Diagnostics.Act("waits (ms)", string.Join(", ", clock.Waits.Select(wait => wait.TotalMilliseconds)));
        Assert.AreEqual(0, exitCode);
        Assert.EndsWith("\r\n\r\n" + Body, written);
        Assert.IsGreaterThanOrEqualTo(2, clock.Waits.Count(wait => wait == TimeSpan.FromSeconds(1)));
        Assert.IsGreaterThanOrEqualTo(TimeSpan.FromSeconds(2), clock.GetElapsedTime(0));
    }

    [TestMethod]
    public async Task RunAsync_DataBodyWithoutLimitRate_SendsItWithoutAWait()
    {
        ScriptedConnector server = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok")]);

        int exitCode = await RunAsync(server, "-d", Body, "http://127.0.0.1:18241/a");

        Assert.AreEqual(0, exitCode);
        Assert.EndsWith(Body, Encoding.Latin1.GetString(server.Written));
        Assert.DoesNotContain(TimeSpan.FromSeconds(1), clock.Waits.ToList());
    }

    private async Task<int> RunAsync(ScriptedConnector server, params string[] arguments)
    {
        HttpProtocolHandler handler = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));
        InMemoryFileSystem outputFiles = new();
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([handler])),
                outputFiles,
                outputFiles,
                new MemoryStream(),
                new MemoryStream(),
                new MemoryStream(),
                runsOnWindows: false,
                writesProgressMeter: false,
                timeProvider: clock)
            .RunAsync(arguments);
        Diagnostics.Act("exit code", exitCode);
        return exitCode;
    }
}
