using System.Net;
using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http;

namespace Curl.Console;

/// <summary>
/// Pins the progress meter the runner writes for a redirect chain, through the redirect
/// follower and <see cref="HttpProtocolHandler" /> over a <see cref="ScriptedConnector" />
/// whose every read takes 40 ms of a manual clock, against curl 8.21.0 (mingw, Schannel)
/// measured on 2026-09-27 with <c>Record-CurlExchange.ps1</c>: <c>/a</c> answered
/// <c>302 Found</c> to <c>/b</c> with an empty body, <c>/b</c> <c>200 OK</c> with
/// <c>hello</c> (task BL-277). Each hop gets a status line of its own: the zero line, then
/// two more draws for a redirect hop or three for the finished transfer, then a newline.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerRedirectProgressMeterTests
{
    private const string Url = "http://127.0.0.1:18246/a";

    private const string Found = "HTTP/1.1 302 Found\r\nLocation: /b\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";

    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nConnection: close\r\n\r\nhello";

    private const string Zero = ProgressMeterLines.ZeroStatusLine;

    /// <summary>Five of five bytes in the 40 ms of one read: 5 * 1000000 / 40000 = 125 bytes per second.</summary>
    private const string FiveOfFiveIn40Milliseconds =
        "\r100      5 100      5   0      0    125      0                              0";

    private static readonly string NewLine = Environment.NewLine;

    private static readonly string HeaderLines =
        "  % Total    % Received % Xferd  Average Speed  Time    Time    Time   Current" + NewLine
        + "                                 Dload  Upload  Total   Spent   Left   Speed" + NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly ManualTimeProvider clock = new();

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_LocationWithIncludeOverA302ThenA200_WritesOneStatusLinePerHop()
    {
        int exitCode = await RunAsync([Found, Ok], "-L", "-i", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            HeaderLines
                + Zero + Zero + Zero + NewLine
                + Zero + FiveOfFiveIn40Milliseconds + FiveOfFiveIn40Milliseconds + FiveOfFiveIn40Milliseconds + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_MaxRedirsZero_WritesTheMeterOpeningBeforeTheExit47Line()
    {
        int exitCode = await RunAsync([Found, Ok], "-L", "--max-redirs", "0", "-i", Url);

        Assert.AreEqual(47, exitCode);
        Assert.AreEqual(
            HeaderLines + Zero + Zero + Zero + NewLine + "curl: (47) Maximum (0) redirects followed" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TwoFollowedRedirects_WritesAStatusLineForEachOfTheThreeHops()
    {
        const string FoundC = "HTTP/1.1 302 Found\r\nLocation: /c\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";

        int exitCode = await RunAsync([Found, FoundC, Ok], "-L", "-o", "out.txt", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            HeaderLines
                + Zero + Zero + Zero + NewLine
                + Zero + Zero + Zero + NewLine
                + Zero + FiveOfFiveIn40Milliseconds + FiveOfFiveIn40Milliseconds + FiveOfFiveIn40Milliseconds + NewLine,
            StandardErrorText);
    }

    /// <summary>
    /// Runs <paramref name="arguments" /> with <see cref="HttpProtocolHandler" /> over a
    /// <see cref="ScriptedConnector" /> serving <paramref name="responses" />, one per
    /// connection, each read advancing <see cref="clock" /> by 40 ms.
    /// </summary>
    private Task<int> RunAsync(string[] responses, params string[] arguments)
    {
        ClockAdvancingConnector server = new(new ScriptedConnector(responses.Select(Encoding.Latin1.GetBytes)), clock);
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));
        InMemoryFileSystem outputFiles = new();

        return new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http])),
                outputFiles,
                outputFiles,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                writesProgressMeter: true,
                timeProvider: clock)
            .RunAsync(arguments);
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
