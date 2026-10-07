using System.Text;

using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins how curl 8.21.0's <c>cookie_setopts</c> joins several <c>-b name=value</c> strings into one
/// <c>Cookie</c> value - <c>;</c> and a space, the space left out before a string that starts with a
/// space or a tab - and its refusal of a joined string of 8200 bytes or more: the wrapped warning
/// unless <c>-s</c>, then exit 100, with no connection made. Measured 2026-10-03 with curl 8.21.0
/// (mingw, Schannel) and <c>Record-CurlExchange.ps1</c> (BL-1391). No test opens a socket.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerCookieStringJoinTests
{
    private const string Url = "http://127.0.0.1:18231/";

    private const string Head = "Host: 127.0.0.1:18231\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n";

    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    private const string TooLongWarning = "Warning: skipped provided cookie, the cookie header would go over 8200 bytes";

    private const string TooLargeError = "curl: (100) A value or data field grew larger than allowed";

    private readonly InMemoryFileSystem fileSystem = new();

    private readonly MemoryStream standardError = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string[] StandardErrorLines =>
        Encoding.UTF8.GetString(standardError.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    [TestMethod]
    public async Task RunAsync_ThreeCookieStringsWithTheEngineOff_JoinsThemAsCurlDoes()
    {
        ScriptedConnector server = Serve(Ok);

        int exitCode = await RunAsync(server, ["-s", "-b", "a=1", "-b", " b=2", "-b", "\tc=3", Url]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("request bytes", $"GET / HTTP/1.1\r\n{Head}Cookie: a=1; b=2;\tc=3\r\n\r\n", Latin1(server.Written));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual($"GET / HTTP/1.1\r\n{Head}Cookie: a=1; b=2;\tc=3\r\n\r\n", Latin1(server.Written));
    }

    [TestMethod]
    public async Task RunAsync_TwoCookieStringsWithTheEngineOn_JoinsThemAsCurlDoes()
    {
        ScriptedConnector server = Serve(Ok);

        int exitCode = await RunAsync(server, ["-s", "-b", "a=1", "-b", " b=2", "-c", "jar.txt", Url]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("request bytes", $"GET / HTTP/1.1\r\n{Head}Cookie: a=1; b=2\r\n\r\n", Latin1(server.Written));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual($"GET / HTTP/1.1\r\n{Head}Cookie: a=1; b=2\r\n\r\n", Latin1(server.Written));
    }

    [TestMethod]
    public async Task RunAsync_CookieStringOf8199Bytes_SendsIt()
    {
        string cookie = CookieString(8199);
        ScriptedConnector server = Serve(Ok);

        int exitCode = await RunAsync(server, ["-sS", "-b", cookie, Url]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("request bytes", $"GET / HTTP/1.1\r\n{Head}Cookie: {cookie}\r\n\r\n", Latin1(server.Written));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual($"GET / HTTP/1.1\r\n{Head}Cookie: {cookie}\r\n\r\n", Latin1(server.Written));
    }

    [TestMethod]
    public async Task RunAsync_TwoCookieStringsJoinedTo8197Bytes_SendsThem()
    {
        string first = CookieString(4098);
        string second = CookieString(4097);
        ScriptedConnector server = Serve(Ok);

        int exitCode = await RunAsync(server, ["-sS", "-b", first, "-b", second, Url]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("request bytes", $"GET / HTTP/1.1\r\n{Head}Cookie: {first}; {second}\r\n\r\n", Latin1(server.Written));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual($"GET / HTTP/1.1\r\n{Head}Cookie: {first}; {second}\r\n\r\n", Latin1(server.Written));
    }

    [TestMethod]
    [DataRow(new[] { 8200 }, DisplayName = "one string of 8200")]
    [DataRow(new[] { 5000, 3198 }, DisplayName = "two strings joined to 8200")]
    public async Task RunAsync_CookieStringsOf8200BytesUnderSilentShowError_FailsWithExit100BeforeConnecting(int[] lengths)
    {
        ScriptedConnector server = Serve(Ok);

        int exitCode = await RunAsync(server, ["-sS", .. CookieArguments(lengths), Url]);

        Diagnostics.Assert("exit code", 100, exitCode);
        Diagnostics.Assert("stderr lines", string.Join(" | ", new[] { TooLargeError, string.Empty }), string.Join(" | ", StandardErrorLines));
        Diagnostics.Assert("connections made", 0, server.Targets.Count());
        Assert.AreEqual(100, exitCode);
        CollectionAssert.AreEqual(new[] { TooLargeError, string.Empty }, StandardErrorLines);
        Assert.IsEmpty(server.Targets);
    }

    [TestMethod]
    [DataRow(new[] { 8200 }, DisplayName = "one string of 8200")]
    [DataRow(new[] { 5002, 4002 }, DisplayName = "two strings over 8200")]
    public async Task RunAsync_CookieStringsOf8200BytesWithoutSilent_WritesTheWarningFirst(int[] lengths)
    {
        ScriptedConnector server = Serve(Ok);

        int exitCode = await RunAsync(server, ["-v", "-o", "out", .. CookieArguments(lengths), Url]);

        string[] lines = StandardErrorLines;
        Diagnostics.Assert("exit code", 100, exitCode);
        Diagnostics.Assert("first stderr line", TooLongWarning, lines[0]);
        Diagnostics.Assert("stderr has the too-large error", true, lines.Contains(TooLargeError));
        Diagnostics.Assert("connections made", 0, server.Targets.Count());
        Assert.AreEqual(100, exitCode);
        Assert.AreEqual(TooLongWarning, lines[0]);
        CollectionAssert.Contains(lines, TooLargeError);
        Assert.IsEmpty(server.Targets);
    }

    [TestMethod]
    public async Task RunAsync_CookieStringOf8200BytesWithAHeaderNamingCookie_StillFailsWithExit100()
    {
        ScriptedConnector server = Serve(Ok);

        int exitCode = await RunAsync(server, ["-s", "-H", "Cookie: x=1", "-b", CookieString(8200), Url]);

        Diagnostics.Assert("exit code", 100, exitCode);
        Diagnostics.Assert("connections made", 0, server.Targets.Count());
        Assert.AreEqual(100, exitCode);
        Assert.IsEmpty(server.Targets);
    }

    /// <summary>A <c>name=value</c> string <paramref name="length" /> bytes long.</summary>
    private static string CookieString(int length) => "a=" + new string('x', length - 2);

    /// <summary>One <c>-b</c> pair per length.</summary>
    private static string[] CookieArguments(int[] lengths) => [.. lengths.SelectMany(length => new[] { "-b", CookieString(length) })];

    private ScriptedConnector Serve(params string[] responses)
    {
        Diagnostics.Arrange(
            "scripted responses",
            string.Join(" | ", responses.Select(response => response.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal))));
        return new(responses.Select(Encoding.Latin1.GetBytes));
    }

    private static string Abbreviate(string argument) =>
        argument.Length > 80 ? $"{argument[..20]}... ({argument.Length} characters)" : argument;

    private async Task<int> RunAsync(IConnector connector, string[] arguments)
    {
        Diagnostics.Arrange("command line", string.Join(" ", arguments.Select(Abbreviate)));

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    options => CreateTransferDispatch(connector, options),
                    fileSystem,
                    fileSystem,
                    new MemoryStream(),
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: true)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stderr", standardError.ToArray());
        if (connector is ScriptedConnector scripted)
        {
            Diagnostics.Bytes("request bytes", scripted.Written);
        }

        return exitCode;
    }

    /// <summary>The production handler set over <paramref name="connector" />, with the run's cookies.</summary>
    private static TransferDispatch CreateTransferDispatch(IConnector connector, Cli.CommandLineOptions options)
    {
        CookieEngine? cookies = CookieEngine.FromCommandLine(options);
        IReadOnlyList<IProtocolHandler> handlers = CurlComposition.CreateProtocolHandlers(
            connector,
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver(),
            cookies?.HandlerStore);

        return new TransferDispatch(new ProtocolDispatcher(handlers), [], cookies);
    }

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);
}
