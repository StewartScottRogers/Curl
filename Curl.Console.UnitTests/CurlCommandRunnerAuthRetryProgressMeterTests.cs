using Curl.Engine;
using System.Net;
using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the progress meter the runner writes for a transfer whose first request is answered
/// <c>401</c> and retried with credentials, through <see cref="HttpProtocolHandler" /> over a
/// <see cref="ScriptedConnector" /> whose every read takes 40 ms of a manual clock. curl 8.21.0
/// (mingw, Schannel) draws each request of such a transfer on a status line of its own, as it
/// draws each redirect hop: the zero line, two more draws for the request answered 401 or three
/// for the finished transfer, then a newline (measured with <c>Record-CurlExchange.ps1</c>,
/// BL-2087 Notes; upstream test1075 and test1072).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerAuthRetryProgressMeterTests
{
    private const string Url = "http://127.0.0.1:18247/a";

    private const string Unauthorized =
        "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"x\"\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";

    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nConnection: close\r\n\r\nhello";

    private const string Zero = ProgressMeterLines.ZeroStatusLine;

    /// <summary>Five of five bytes in the 40 ms of one read: 5 * 1000000 / 40000 = 125 bytes per second.</summary>
    private const string FiveOfFiveIn40Milliseconds =
        "\r100      5 100      5   0      0    125      0                              0";

    /// <summary>
    /// Five of five bytes 80 ms after the transfer started, as the retried request keeps the
    /// transfer's start (BL-2131 Notes): 5 * 1000000 / 80000 = 62 bytes per second.
    /// </summary>
    private const string FiveOfFiveIn80Milliseconds =
        "\r100      5 100      5   0      0     62      0                              0";

    private static readonly string NewLine = Environment.NewLine;

    private static readonly string HeaderLines =
        "  % Total    % Received % Xferd  Average Speed  Time    Time    Time   Current" + NewLine
        + "                                 Dload  Upload  Total   Spent   Left   Speed" + NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly ManualTimeProvider clock = new();

    private Stream standardInput = new MemoryStream();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_401ThenRetriedWithAnyAuth_WritesAStatusLineForEachRequest()
    {
        int exitCode = await RunAsync([Unauthorized, Ok], "--anyauth", "-u", "testuser:testpass", "-o", "out.txt", Url);

        string expected = HeaderLines
            + Zero + Zero + Zero + NewLine
            + Zero + FiveOfFiveIn80Milliseconds + FiveOfFiveIn80Milliseconds + FiveOfFiveIn80Milliseconds + NewLine;
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Lf(expected), Lf(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(expected, StandardErrorText);
    }

    /// <summary>
    /// A 401 from an HTTP/1.0 server to a <c>-T -</c> upload: curl 8.21.0 reconnects for the
    /// retry and draws it a new zero status line before failing it with exit 25 (upstream
    /// test1072, BL-2130), so the meter has two lines.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_Http10401ToAStdinUpload_WritesAStatusLineForTheRetryThenExits25()
    {
        standardInput = new MemoryStream("hello"u8.ToArray());
        string http10Unauthorized = Unauthorized.Replace("HTTP/1.1", "HTTP/1.0", StringComparison.Ordinal);

        int exitCode = await RunAsync([http10Unauthorized, Ok], "-T", "-", "--anyauth", "-u", "testuser:testpass", "-o", "out.txt", Url);

        string[] meterLines = Lf(StandardErrorText).Split('\n')[2..^1];
        Diagnostics.Assert("exit code", 25, exitCode);
        Diagnostics.Assert("status lines", 2, meterLines.Count(line => line.StartsWith(Zero, StringComparison.Ordinal)));
        Assert.AreEqual(25, exitCode);
        Assert.AreEqual(2, meterLines.Count(line => line.StartsWith(Zero, StringComparison.Ordinal)));
        Assert.EndsWith("curl: (25) Chunky upload is not supported by HTTP 1.0" + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_NoRetry_WritesOneStatusLine()
    {
        int exitCode = await RunAsync([Ok], "-u", "testuser:testpass", "-o", "out.txt", Url);

        string expected = HeaderLines
            + Zero + FiveOfFiveIn40Milliseconds + FiveOfFiveIn40Milliseconds + FiveOfFiveIn40Milliseconds + NewLine;
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Lf(expected), Lf(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(expected, StandardErrorText);
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>
    /// Runs <paramref name="arguments" /> with <see cref="HttpProtocolHandler" /> over a
    /// <see cref="ScriptedConnector" /> serving <paramref name="responses" />, one per
    /// connection, each read advancing <see cref="clock" /> by 40 ms.
    /// </summary>
    private async Task<int> RunAsync(string[] responses, params string[] arguments)
    {
        ClockAdvancingConnector server = new(new ScriptedConnector(responses.Select(Encoding.Latin1.GetBytes)), clock);
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));
        InMemoryFileSystem outputFiles = new();
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("scripted responses", Lf(string.Join(" | ", responses)));
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([http])),
                    outputFiles,
                    outputFiles,
                    standardOutput,
                    standardError,
                    standardInput,
                    runsOnWindows: false,
                    writesProgressMeter: true,
                    timeProvider: clock)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stderr", Lf(StandardErrorText));
        return exitCode;
    }

    /// <summary>A connector whose connections advance the clock by 40 ms on every read that returns bytes.</summary>
    private sealed class ClockAdvancingConnector(IConnector inner, ManualTimeProvider clock) : IConnector
    {
        public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            ConnectResult result = await inner.ConnectAsync(target, cancellationToken);

            return ConnectResult.Connected(new ClockAdvancingConnection(result.Connection!, clock));
        }
    }

    private sealed class ClockAdvancingConnection(IConnection inner, ManualTimeProvider clock) : IConnection
    {
        public bool IsSecure => inner.IsSecure;

        public EndPoint? RemoteEndPoint => inner.RemoteEndPoint;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int count = await inner.ReadAsync(buffer, cancellationToken);
            if (count > 0)
            {
                clock.Advance(40);
            }

            return count;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
            inner.WriteAsync(buffer, cancellationToken);

        public ValueTask FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
