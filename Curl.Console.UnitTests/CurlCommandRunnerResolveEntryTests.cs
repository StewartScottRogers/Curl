using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that the runner loads the <c>--resolve</c> entries at the start of every URL's transfer,
/// and not for a followed redirect, against curl 8.21.0 (mingw, Schannel) measured on 2026-09-27
/// with <c>Record-CurlExchange.ps1</c>, <c>-s -v</c> and <c>Connection: close</c> responses (BL-486
/// Notes). The transfers run through the production handler set over a real
/// <see cref="TcpConnector" /> dialing a <see cref="ScriptedTcpDialer" />.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerResolveEntryTests
{
    private const string InfoEnd = "\r\n";

    private readonly MemoryStream standardError = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.ASCII.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_TwoUrlsWithOneResolveEntry_ReloadsTheEntryBeforeTheSecondTransfer()
    {
        int exitCode = await RunAsync(
            [Response("200 OK", string.Empty), Response("200 OK", string.Empty)],
            "--resolve", "foo.example:18486:127.0.0.1", "http://foo.example:18486/a", "http://foo.example:18486/b");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string secondTransfer = StandardErrorText[StandardErrorText.IndexOf("* shutting down connection #0" + InfoEnd, StringComparison.Ordinal)..];
        Diagnostics.Assert("second transfer starts with the reload lines", true, secondTransfer.StartsWith("* shutting down connection #0" + InfoEnd + "* RESOLVE foo.example:18486 - old addresses discarded" + InfoEnd + "* Added foo.example:18486:127.0.0.1 to DNS cache" + InfoEnd + "* Hostname foo.example was found in DNS cache" + InfoEnd, StringComparison.Ordinal));
        StringAssert.StartsWith(
            secondTransfer,
            "* shutting down connection #0" + InfoEnd
            + "* RESOLVE foo.example:18486 - old addresses discarded" + InfoEnd
            + "* Added foo.example:18486:127.0.0.1 to DNS cache" + InfoEnd
            + "* Hostname foo.example was found in DNS cache" + InfoEnd);
        Diagnostics.Assert("occurrences of " + "* Added foo.example:18486:127.0.0.1 to DNS cache", 2, Occurrences("* Added foo.example:18486:127.0.0.1 to DNS cache"));
        Assert.AreEqual(2, Occurrences("* Added foo.example:18486:127.0.0.1 to DNS cache"));
    }

    [TestMethod]
    public async Task RunAsync_FollowedRedirectWithOneResolveEntry_LoadsTheEntryOnce()
    {
        int exitCode = await RunAsync(
            [Response("302 Found", "Location: /b\r\n"), Response("200 OK", string.Empty)],
            "-L", "--resolve", "foo.example:18486:127.0.0.1", "http://foo.example:18486/a");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("stderr starts with the added line", true, StandardErrorText.StartsWith("* Added foo.example:18486:127.0.0.1 to DNS cache" + InfoEnd, StringComparison.Ordinal));
        StringAssert.StartsWith(StandardErrorText, "* Added foo.example:18486:127.0.0.1 to DNS cache" + InfoEnd);
        Diagnostics.Assert("occurrences of " + "Added foo.example", 1, Occurrences("Added foo.example"));
        Assert.AreEqual(1, Occurrences("Added foo.example"));
        Diagnostics.Assert("occurrences of " + "RESOLVE foo.example", 0, Occurrences("RESOLVE foo.example"));
        Assert.AreEqual(0, Occurrences("RESOLVE foo.example"));
        Diagnostics.Assert("occurrences of " + "* Hostname foo.example was found in DNS cache", 2, Occurrences("* Hostname foo.example was found in DNS cache"));
        Assert.AreEqual(2, Occurrences("* Hostname foo.example was found in DNS cache"));
    }

    [TestMethod]
    public void LoadResolveEntries_WithoutALoader_DoesNothing()
    {
        TransferDispatch dispatch = new(new ProtocolDispatcher([]));

        Diagnostics.Arrange("dispatch", "no resolve-entry loader");
        using (Diagnostics.Phase("load"))
        {
            dispatch.LoadResolveEntries(NoTransferEvents.Instance);
        }

        Diagnostics.Act("connection pool", dispatch.ConnectionPool is null ? "null" : "created");

        Diagnostics.Assert("connection pool is null", true, dispatch.ConnectionPool is null);
        Assert.IsNull(dispatch.ConnectionPool);
    }

    private static byte[] Response(string status, string headers) =>
        Encoding.Latin1.GetBytes($"HTTP/1.1 {status}\r\n{headers}Content-Length: 0\r\nConnection: close\r\n\r\n");

    private int Occurrences(string text) =>
        (StandardErrorText.Length - StandardErrorText.Replace(text, string.Empty, StringComparison.Ordinal).Length) / text.Length;

    /// <summary>
    /// Runs <c>-s -v</c> and <paramref name="arguments" /> through the production handler set over
    /// one <see cref="TcpConnector" /> built as the composition builds it, whose connections replay
    /// <paramref name="responses" />, with that connector's <see cref="TcpConnector.LoadResolveEntries" />
    /// as the dispatch's loader.
    /// </summary>
    private async Task<int> RunAsync(byte[][] responses, params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Diagnostics.Assert("command line accepted", true, parsed.IsAccepted);
        Assert.IsTrue(parsed.IsAccepted);
        TcpConnector connector = CurlComposition.CreateTcpConnector(
            parsed.Options,
            new LoopbackDnsResolver(),
            new ScriptedTcpDialer(new ScriptedConnector(responses)),
            new PassThroughTlsProvider(),
            TimeProvider.System,
            HttpProxyTunnelOptions.Default);
        InMemoryFileSystem files = new();

        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("scripted responses", Lf(string.Join("\n--\n", responses.Select(Encoding.Latin1.GetString))));
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(
                        new ProtocolDispatcher(CurlComposition.CreateProtocolHandlers(connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver())),
                        [],
                        loadResolveEntries: connector.LoadResolveEntries),
                    files,
                    files,
                    new MemoryStream(),
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: true)
                .RunAsync(["-s", "-v", .. arguments]);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stderr", Lf(StandardErrorText));
        return exitCode;
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);
}
