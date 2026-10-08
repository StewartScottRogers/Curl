using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that every <c>-:</c>/<c>--next</c> option group's <see cref="TcpConnector" /> answers from
/// the run's one <see cref="DnsCache" />, so a later group finds a host an earlier group resolved, or
/// an earlier group's <c>--resolve</c> entry, in it, against curl 8.21.0 (mingw, Schannel) measured
/// on 2026-10-02 with <c>Record-CurlExchange.ps1</c>, <c>-sv</c> and <c>Connection: close</c>
/// responses (BL-1053 Notes).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerSharedDnsCacheTests
{
    private const string InfoEnd = "\r\n";

    private readonly MemoryStream standardError = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.ASCII.GetString(standardError.ToArray());

    [TestMethod]
    public void CreateTransports_TwoOptionGroupsGivenTheRunsDnsCache_BothTcpConnectorsAnswerFromIt()
    {
        DnsCache runDnsCache = new();

        Diagnostics.Arrange("first option group", "http://h/a");
        Diagnostics.Arrange("second option group", "-k https://h/b");
        CurlTransports first = CurlComposition.CreateTransports(Parse("http://h/a"), TimeProvider.System, runDnsCache: runDnsCache);
        CurlTransports second = CurlComposition.CreateTransports(Parse("-k", "https://h/b"), TimeProvider.System, runDnsCache: runDnsCache);

        Diagnostics.Act("first connector uses the run cache", ReferenceEquals(runDnsCache, first.TcpConnector.DnsCache));
        Diagnostics.Act("second connector uses the run cache", ReferenceEquals(runDnsCache, second.TcpConnector.DnsCache));
        Diagnostics.Assert("first connector uses the run cache", true, ReferenceEquals(runDnsCache, first.TcpConnector.DnsCache));
        Diagnostics.Assert("second connector uses the run cache", true, ReferenceEquals(runDnsCache, second.TcpConnector.DnsCache));
        Assert.AreSame(runDnsCache, first.TcpConnector.DnsCache);
        Assert.AreSame(runDnsCache, second.TcpConnector.DnsCache);
    }

    [TestMethod]
    public void CreateTransports_WithoutARunDnsCache_GivesEachTcpConnectorACacheOfItsOwn()
    {
        Diagnostics.Arrange("option groups", "http://h/a and http://h/b, each without a run cache");
        CurlTransports first = CurlComposition.CreateTransports(Parse("http://h/a"));
        CurlTransports second = CurlComposition.CreateTransports(Parse("http://h/b"));

        Diagnostics.Act("connectors share a cache", ReferenceEquals(first.TcpConnector.DnsCache, second.TcpConnector.DnsCache));
        Diagnostics.Assert("connectors share a cache", false, ReferenceEquals(first.TcpConnector.DnsCache, second.TcpConnector.DnsCache));
        Assert.AreNotSame(first.TcpConnector.DnsCache, second.TcpConnector.DnsCache);
    }

    [TestMethod]
    public async Task RunAsync_LaterGroupToAnIpAddressAnEarlierGroupDialled_ReportsItFoundInDnsCacheOnlyThere()
    {
        // curl -sv http://127.0.0.1:18531/a --next -sv http://127.0.0.1:18531/b -> only group 2 prints
        // * Hostname 127.0.0.1 was found in DNS cache, right before *   Trying 127.0.0.1:18531...
        int exitCode = await RunAsync("http://127.0.0.1:18531/a", "--next", "-s", "-v", "http://127.0.0.1:18531/b");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("found-in-cache then trying", 1, Occurrences("* Hostname 127.0.0.1 was found in DNS cache" + InfoEnd + "*   Trying 127.0.0.1:18531..." + InfoEnd));
        Diagnostics.Assert("found-in-cache lines", 1, Occurrences("Hostname 127.0.0.1 was found in DNS cache"));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(1, Occurrences("* Hostname 127.0.0.1 was found in DNS cache" + InfoEnd + "*   Trying 127.0.0.1:18531..." + InfoEnd));
        Assert.AreEqual(1, Occurrences("Hostname 127.0.0.1 was found in DNS cache"));
    }

    [TestMethod]
    public async Task RunAsync_LaterGroupWithoutTheEarlierGroupsResolveEntry_StillAnswersFromIt()
    {
        // curl -sv --resolve foo.example:18531:127.0.0.1 http://foo.example:18531/a --next
        // -sv http://foo.example:18531/b -> the entry is added once, and found in the cache by both groups.
        int exitCode = await RunAsync(
            "--resolve", "foo.example:18531:127.0.0.1", "http://foo.example:18531/a", "--next", "-s", "-v", "http://foo.example:18531/b");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("added lines", 1, Occurrences("* Added foo.example:18531:127.0.0.1 to DNS cache" + InfoEnd));
        Diagnostics.Assert(
            "found-in-cache blocks",
            2,
            Occurrences(
                "* Hostname foo.example was found in DNS cache" + InfoEnd
                + "* Host foo.example:18531 was resolved." + InfoEnd
                + "* IPv6: (none)" + InfoEnd
                + "* IPv4: 127.0.0.1" + InfoEnd
                + "*   Trying 127.0.0.1:18531..." + InfoEnd));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(1, Occurrences("* Added foo.example:18531:127.0.0.1 to DNS cache" + InfoEnd));
        Assert.AreEqual(
            2,
            Occurrences(
                "* Hostname foo.example was found in DNS cache" + InfoEnd
                + "* Host foo.example:18531 was resolved." + InfoEnd
                + "* IPv6: (none)" + InfoEnd
                + "* IPv4: 127.0.0.1" + InfoEnd
                + "*   Trying 127.0.0.1:18531..." + InfoEnd));
    }

    [TestMethod]
    public async Task RunAsync_LaterGroupsResolveEntryForAHostAnEarlierGroupResolved_DiscardsTheOldAddresses()
    {
        // curl -sv http://localhost:18531/a --next -sv --resolve localhost:18531:127.0.0.1
        // http://localhost:18531/b -> group 2: * RESOLVE localhost:18531 - old addresses discarded /
        // * Added localhost:18531:127.0.0.1 to DNS cache / * Hostname localhost was found in DNS cache
        int exitCode = await RunAsync(
            "http://localhost:18531/a", "--next", "-s", "-v", "--resolve", "localhost:18531:127.0.0.1", "http://localhost:18531/b");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert(
            "discard blocks",
            1,
            Occurrences(
                "* RESOLVE localhost:18531 - old addresses discarded" + InfoEnd
                + "* Added localhost:18531:127.0.0.1 to DNS cache" + InfoEnd
                + "* Hostname localhost was found in DNS cache" + InfoEnd
                + "* Host localhost:18531 was resolved." + InfoEnd));
        Diagnostics.Assert("found-in-cache lines", 1, Occurrences("Hostname localhost was found in DNS cache"));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            1,
            Occurrences(
                "* RESOLVE localhost:18531 - old addresses discarded" + InfoEnd
                + "* Added localhost:18531:127.0.0.1 to DNS cache" + InfoEnd
                + "* Hostname localhost was found in DNS cache" + InfoEnd
                + "* Host localhost:18531 was resolved." + InfoEnd));
        Assert.AreEqual(1, Occurrences("Hostname localhost was found in DNS cache"));
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }

    private static byte[] ClosingResponse() =>
        Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");

    private int Occurrences(string text) =>
        (StandardErrorText.Length - StandardErrorText.Replace(text, string.Empty, StringComparison.Ordinal).Length) / text.Length;

    /// <summary>
    /// Runs <c>-s -v</c> and <paramref name="arguments" /> through the production handler set, each
    /// option group over a <see cref="TcpConnector" /> built as the composition builds it, all of
    /// them over one run <see cref="DnsCache" />, dialing one <see cref="ScriptedTcpDialer" /> whose
    /// connections each answer with a closing <c>200</c>.
    /// </summary>
    private async Task<int> RunAsync(params string[] arguments)
    {
        DnsCache runDnsCache = new();
        ScriptedTcpDialer dialer = new(new ScriptedConnector([ClosingResponse(), ClosingResponse()]));
        InMemoryFileSystem files = new();
        Diagnostics.Arrange("arguments", string.Join(' ', ["-s", "-v", .. arguments]));
        Diagnostics.Arrange("dialer", "one scripted dialer whose connections each answer with a closing 200");
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await RunWithAsync(runDnsCache, dialer, files, arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stderr", StandardErrorText.Replace("\r\n", "\n", StringComparison.Ordinal));
        return exitCode;
    }

    private async Task<int> RunWithAsync(DnsCache runDnsCache, ScriptedTcpDialer dialer, InMemoryFileSystem files, string[] arguments)
    {
        return await new CurlCommandRunner(
                options =>
                {
                    TcpConnector connector = CurlComposition.CreateTcpConnector(
                        options,
                        new LoopbackDnsResolver(),
                        dialer,
                        new PassThroughTlsProvider(),
                        TimeProvider.System,
                        HttpProxyTunnelOptions.Default,
                        runDnsCache: runDnsCache);
                    return new TransferDispatch(
                        new ProtocolDispatcher(CurlComposition.CreateProtocolHandlers(connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver())),
                        [],
                        loadResolveEntries: connector.LoadResolveEntries);
                },
                files,
                files,
                new MemoryStream(),
                standardError,
                new MemoryStream(),
                runsOnWindows: true)
            .RunAsync(["-s", "-v", .. arguments]);
    }
}
