using System.Text;
using Curl.Protocol.Abstractions;

using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Runs <c>--ntlm</c> and <c>--anyauth</c> through the production composition and HTTP handler
/// over a <see cref="ScriptedConnector" />, as both platform curls ran them on 2026-09-28
/// against a loopback server (BL-526 Notes): Type 1 on the first request, the Type 2 challenge
/// answered with Type 3 on the same connection, and the ways the handshake stops. The scripted
/// token source stands in for SSPI and the hand-built NTLM; the production router is run too,
/// each platform pinned to its own curl.
/// </summary>
[TestClass]
public sealed class CurlCompositionNtlmTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string Url = "http://127.0.0.1:18526/x";

    private const string Request = "GET /x HTTP/1.1\r\nHost: 127.0.0.1:18526\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";

    private const string Type2 = "TlRMTVNTUAACAAAADAAMADgAAAAzgoriASNFZ4mrze8AAAAAAAAAACQAJABEAAAABgBwFwAAAA9TAGUAcgB2AGUAcgACAAwARABvAG0AYQBpAG4AAQAMAFMAZQByAHYAZQByAAAAAAAA";

    private const string Type2Challenge = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: NTLM " + Type2 + "\r\nContent-Length: 4\r\n\r\nnope";

    private const string BareChallenge = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: NTLM\r\nContent-Length: 4\r\n\r\nnope";

    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";

    [TestMethod]
    public async Task RunAsync_NtlmThreeLegs_SendsType1ThenType3OnOneConnection()
    {
        TokenSource tokens = new();
        ScriptedConnector server = Server(Type2Challenge, Ok);

        CurlRun run = await RunAsync(server, tokens, "--ntlm", "-u", "u:p", Url);

        Diagnostics.Assert("run.ExitCode", 0, run.ExitCode);
        Assert.AreEqual(0, run.ExitCode);
        Diagnostics.Assert("Latin1(server.Written)", WithNtlm("AQ==") + WithNtlm("Aw=="), Latin1(server.Written));
        Assert.AreEqual(WithNtlm("AQ==") + WithNtlm("Aw=="), Latin1(server.Written));
        Diagnostics.Assert("run.StandardOutput", "ok", run.StandardOutput);
        Assert.AreEqual("ok", run.StandardOutput);
        Assert.HasCount(1, server.Targets);
        CollectionAssert.AreEqual(Convert.FromBase64String(Type2), tokens.Contexts[1].IncomingTokens[1]);
        Diagnostics.Assert("tokens.Requests[1]", new SecurityContextRequest(SecurityMechanism.Ntlm, "HTTP", "127.0.0.1") { UserName = "u", Password = "p" }, tokens.Requests[1]);
        Assert.AreEqual(new SecurityContextRequest(SecurityMechanism.Ntlm, "HTTP", "127.0.0.1") { UserName = "u", Password = "p" }, tokens.Requests[1]);
    }

    [TestMethod]
    public async Task RunAsync_ProxyNtlmThroughAForwardProxy_SendsType1ThenType3OnOneConnection()
    {
        // Measured (BL-604 Notes): curl -x http://127.0.0.1:18605 --proxy-ntlm -U u:p http://example.test/.
        const string proxyRequest = "GET http://example.test/ HTTP/1.1\r\nHost: example.test\r\nProxy-Authorization: NTLM {0}\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\n\r\n";
        TokenSource tokens = new();
        ScriptedConnector server = Server("HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: NTLM " + Type2 + "\r\nContent-Length: 0\r\n\r\n", Ok);

        CurlRun run = await RunAsync(server, tokens, "-x", "http://127.0.0.1:18605", "--proxy-ntlm", "-U", "u:p", "http://example.test/");

        Diagnostics.Assert("run.ExitCode", 0, run.ExitCode);
        Assert.AreEqual(0, run.ExitCode);
        Diagnostics.Assert("Latin1(server.Written)", string.Format(null, proxyRequest, "AQ==") + string.Format(null, proxyRequest, "Aw=="), Latin1(server.Written));
        Assert.AreEqual(string.Format(null, proxyRequest, "AQ==") + string.Format(null, proxyRequest, "Aw=="), Latin1(server.Written));
        Diagnostics.Assert("run.StandardOutput", "ok", run.StandardOutput);
        Assert.AreEqual("ok", run.StandardOutput);
        Assert.HasCount(1, server.Targets);
        CollectionAssert.AreEqual(Convert.FromBase64String(Type2), tokens.Contexts[1].IncomingTokens[1]);
        Diagnostics.Assert("tokens.Requests[1]", new SecurityContextRequest(SecurityMechanism.Ntlm, "HTTP", "127.0.0.1") { UserName = "u", Password = "p" }, tokens.Requests[1]);
        Assert.AreEqual(new SecurityContextRequest(SecurityMechanism.Ntlm, "HTTP", "127.0.0.1") { UserName = "u", Password = "p" }, tokens.Requests[1]);
    }

    [TestMethod]
    public async Task RunAsync_AnyauthOfferedBasicAndNtlm_AnswersNtlmInThreeLegs()
    {
        TokenSource tokens = new();
        ScriptedConnector server = Server("HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"r\"\r\nWWW-Authenticate: NTLM\r\nContent-Length: 4\r\n\r\nnope", Type2Challenge, Ok);

        CurlRun run = await RunAsync(server, tokens, "--anyauth", "-u", "u:p", Url);

        Diagnostics.Assert("run.ExitCode", 0, run.ExitCode);
        Assert.AreEqual(0, run.ExitCode);
        Diagnostics.Assert("Latin1(server.Written)", Request + WithNtlm("AQ==") + WithNtlm("Aw=="), Latin1(server.Written));
        Assert.AreEqual(Request + WithNtlm("AQ==") + WithNtlm("Aw=="), Latin1(server.Written));
        Diagnostics.Assert("run.StandardOutput", "ok", run.StandardOutput);
        Assert.AreEqual("ok", run.StandardOutput);
        Assert.HasCount(1, server.Targets);
    }

    [TestMethod]
    public async Task RunAsync_BareNtlmAfterType1SentUpFront_SendsType1OnceMoreThenStops()
    {
        ScriptedConnector server = Server(BareChallenge, BareChallenge);

        CurlRun run = await RunAsync(server, new TokenSource(), "--ntlm", "-u", "u:p", Url);

        Diagnostics.Assert("run.ExitCode", 0, run.ExitCode);
        Assert.AreEqual(0, run.ExitCode);
        Diagnostics.Assert("Latin1(server.Written)", WithNtlm("AQ==") + WithNtlm("AQ=="), Latin1(server.Written));
        Assert.AreEqual(WithNtlm("AQ==") + WithNtlm("AQ=="), Latin1(server.Written));
        Diagnostics.Assert("run.StandardOutput", "nope", run.StandardOutput);
        Assert.AreEqual("nope", run.StandardOutput);
    }

    [TestMethod]
    public async Task RunAsync_ServerRejectsType3_WritesThe401AsCurlsHandshakeRejectedDoes()
    {
        ScriptedConnector server = Server(Type2Challenge, BareChallenge);

        CurlRun run = await RunAsync(server, new TokenSource(), "--ntlm", "-u", "u:p", Url);

        Diagnostics.Assert("run.ExitCode", 0, run.ExitCode);
        Assert.AreEqual(0, run.ExitCode);
        Diagnostics.Assert("Latin1(server.Written)", WithNtlm("AQ==") + WithNtlm("Aw=="), Latin1(server.Written));
        Assert.AreEqual(WithNtlm("AQ==") + WithNtlm("Aw=="), Latin1(server.Written));
        Diagnostics.Assert("run.StandardOutput", "nope", run.StandardOutput);
        Assert.AreEqual("nope", run.StandardOutput);
        Assert.IsEmpty(run.StandardError);
    }

    [TestMethod]
    public async Task RunAsync_NtlmWithoutUser_SendsNoAuthorization()
    {
        TokenSource tokens = new();
        ScriptedConnector server = Server(BareChallenge);

        CurlRun run = await RunAsync(server, tokens, "--ntlm", Url);

        Diagnostics.Assert("run.ExitCode", 0, run.ExitCode);
        Assert.AreEqual(0, run.ExitCode);
        Diagnostics.Assert("Latin1(server.Written)", Request, Latin1(server.Written));
        Assert.AreEqual(Request, Latin1(server.Written));
        Assert.IsEmpty(tokens.Requests);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task RunAsync_WindowsProductionRoute_SendsSspisType1AndType3AsCurl8210Measured()
    {
        ScriptedConnector server = Server(Type2Challenge, Ok);

        CurlRun run = await RunAsync(server, null, "--ntlm", "-u", "u:p", Url);

        Diagnostics.Assert("run.ExitCode", 0, run.ExitCode);
        Assert.AreEqual(0, run.ExitCode);
        string[] authorizations = AuthorizationsOf(server);
        StringAssert.StartsWith(authorizations[0], "NTLM TlRMTVNTUAABAAAAB4IIog");
        StringAssert.StartsWith(authorizations[1], "NTLM TlRMTVNTUAADAAAAGAAYA");
        Diagnostics.Assert("run.StandardOutput", "ok", run.StandardOutput);
        Assert.AreEqual("ok", run.StandardOutput);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task RunAsync_ProductionRouteOffWindows_SendsCurlsOwnType1AndType3AsCurl8180Measured()
    {
        ScriptedConnector server = Server(Type2Challenge, Ok);

        CurlRun run = await RunAsync(server, null, "--ntlm", "-u", "u:p", Url);

        Diagnostics.Assert("run.ExitCode", 0, run.ExitCode);
        Assert.AreEqual(0, run.ExitCode);
        string[] authorizations = AuthorizationsOf(server);
        Diagnostics.Assert("authorizations[0]", "NTLM TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=", authorizations[0]);
        Assert.AreEqual("NTLM TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=", authorizations[0]);
        StringAssert.StartsWith(authorizations[1], "NTLM TlRMTVNTUAADAAAAGAAYAEAAAABUAFQAWAAAAAAAAACsAAAAAgACAKwAAAAWABYArgAAAAAAAAAAAAAAM4KK4");
        Diagnostics.Assert("run.StandardOutput", "ok", run.StandardOutput);
        Assert.AreEqual("ok", run.StandardOutput);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task RunAsync_WindowsType2SspiCannotRead_FailsWithExit94AsCurl8210Measured()
    {
        ScriptedConnector server = Server(Type2Challenge.Replace(Type2, "TlRMTVNTUAACAAAA", StringComparison.Ordinal));

        CurlRun run = await RunAsync(server, null, "--ntlm", "-u", "u:p", Url);

        Diagnostics.Assert("run.ExitCode", 94, run.ExitCode);
        Assert.AreEqual(94, run.ExitCode);
        Assert.IsEmpty(run.StandardOutput);
        Diagnostics.Assert("run.StandardError", ("curl: (94) An authentication function returned an error" + Environment.NewLine).ReplaceLineEndings("\n"), run.StandardError.ReplaceLineEndings("\n"));
        Assert.AreEqual("curl: (94) An authentication function returned an error" + Environment.NewLine, run.StandardError);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task RunAsync_Type2CurlCannotReadOffWindows_WritesThe401AsCurl8180Measured()
    {
        ScriptedConnector server = Server(Type2Challenge.Replace(Type2, "TlRMTVNTUAACAAAA", StringComparison.Ordinal));

        CurlRun run = await RunAsync(server, null, "--ntlm", "-u", "u:p", Url);

        Diagnostics.Assert("run.ExitCode", 0, run.ExitCode);
        Assert.AreEqual(0, run.ExitCode);
        Diagnostics.Assert("run.StandardOutput", "nope", run.StandardOutput);
        Assert.AreEqual("nope", run.StandardOutput);
        Assert.IsEmpty(run.StandardError);
    }

    private static ScriptedConnector Server(params string[] responses) =>
        new([.. responses.Select(Encoding.Latin1.GetBytes)]);

    private static string WithNtlm(string token) =>
        Request.Replace("User-Agent", "Authorization: NTLM " + token + "\r\nUser-Agent", StringComparison.Ordinal);

    private static string[] AuthorizationsOf(ScriptedConnector server) =>
        [.. Latin1(server.Written).Split("\r\n").Where(line => line.StartsWith("Authorization: ", StringComparison.Ordinal)).Select(line => line["Authorization: ".Length..])];

    private async Task<CurlRun> RunAsync(ScriptedConnector server, ISecurityContextFactory? tokens, params string[] arguments)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        using MemoryStream standardInput = new();

        Diagnostics.ArrangeCommandLine(["-sS", .. arguments]);
        int exitCode = await CurlComposition
            .CreateRunner(
                standardOutput,
                standardError,
                standardInput,
                server,
                new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"),
                securityContexts: tokens)
            .RunAsync(["-sS", .. arguments]);

        CurlRun run = new(exitCode, Latin1(standardOutput.ToArray()), Encoding.UTF8.GetString(standardError.ToArray()));
        Diagnostics.ActRun(run.ExitCode, run.StandardOutput, run.StandardError);
        Diagnostics.ActWritten(server);
        return run;
    }

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    private sealed record CurlRun(int ExitCode, string StandardOutput, string StandardError);

    /// <summary>
    /// Hands out, for every request, a context whose steps are Type 1 (<c>AQ==</c>) then Type 3
    /// (<c>Aw==</c>), and records the requests and the contexts.
    /// </summary>
    private sealed class TokenSource : ISecurityContextFactory
    {
        public List<SecurityContextRequest> Requests { get; } = [];

        public List<TwoStepContext> Contexts { get; } = [];

        public ISecurityContext Create(SecurityContextRequest request)
        {
            Requests.Add(request);
            TwoStepContext context = new();
            Contexts.Add(context);
            return context;
        }
    }

    private sealed class TwoStepContext : ISecurityContext
    {
        public List<byte[]> IncomingTokens { get; } = [];

        public bool IsCompleted => IncomingTokens.Count == 2;

        public ValueTask<SecurityContextStep> NextTokenAsync(ReadOnlyMemory<byte> incomingToken, CancellationToken cancellationToken)
        {
            IncomingTokens.Add(incomingToken.ToArray());
            return ValueTask.FromResult(IncomingTokens.Count == 1
                ? new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01])
                : new SecurityContextStep(SecurityContextStatus.Completed, [0x03]));
        }

        public byte[]? Wrap(ReadOnlySpan<byte> message, bool encrypt) => throw new NotSupportedException();

        public byte[]? Unwrap(ReadOnlySpan<byte> wrappedMessage) => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }
}
