using System.Text;
using Curl.Authentication;
using Curl.Cli;
using Curl.Protocol.Abstractions;

using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Runs <c>--negotiate</c> and <c>--anyauth</c> through the production composition and HTTP
/// handler over a <see cref="ScriptedConnector" />, with a scripted token source in place of
/// SSPI, GSS-API and the KDC; and, with the production router, the failure both platform
/// curls showed on 2026-09-28 against a <c>401</c> with <c>WWW-Authenticate: Negotiate</c> and
/// no ticket: one request, no <c>Authorization</c>, the 401's body, exit 0 (BL-527 Notes).
/// </summary>
[TestClass]
public sealed class CurlCompositionNegotiateTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string Url = "http://127.0.0.1:18527/p";

    private const string Request = "GET /p HTTP/1.1\r\nHost: 127.0.0.1:18527\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";

    private const string Unauthorized = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Negotiate\r\nContent-Length: 4\r\n\r\ndeny";

    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello";

    [TestMethod]
    public async Task RunAsync_NegotiateAloneWithATicket_SendsTheTokenOnTheFirstRequest()
    {
        TokenSource tokens = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x60, 0x01, 0x00]));
        ScriptedConnector server = new([Encoding.Latin1.GetBytes(Ok)]);

        (int exitCode, string standardOutput) = await RunAsync(server, tokens, "--negotiate", "-u", ":", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Request.Replace("User-Agent", "Authorization: Negotiate YAEA\r\nUser-Agent", StringComparison.Ordinal), Latin1(server.Written));
        Assert.AreEqual("hello", standardOutput);
        Assert.AreEqual(new SecurityContextRequest(SecurityMechanism.Negotiate, "HTTP", "127.0.0.1"), tokens.Requests.Single());
    }

    [TestMethod]
    public async Task RunAsync_ServiceNameAndDelegation_ReachTheAuthenticatorsContextRequest()
    {
        TokenSource tokens = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x60, 0x01, 0x00]));
        ScriptedConnector server = new([Encoding.Latin1.GetBytes(Ok)]);

        (int exitCode, _) = await RunAsync(server, tokens, "--negotiate", "-u", ":", "--service-name", "svc", "--proxy-service-name", "proxysvc", "--delegation", "always", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(new SecurityContextRequest(SecurityMechanism.Negotiate, "svc", "127.0.0.1") { Delegation = SecurityDelegation.Always }, tokens.Requests.Single());
    }

    [TestMethod]
    [DataRow(new string[0], null, null, SecurityDelegation.None, DisplayName = "none given")]
    [DataRow(new[] { "--service-name", "svc" }, "svc", null, SecurityDelegation.None, DisplayName = "--service-name")]
    [DataRow(new[] { "--proxy-service-name", "proxysvc" }, null, "proxysvc", SecurityDelegation.None, DisplayName = "--proxy-service-name")]
    [DataRow(new[] { "--delegation", "policy" }, null, null, SecurityDelegation.Policy, DisplayName = "--delegation policy")]
    [DataRow(new[] { "--delegation", "always" }, null, null, SecurityDelegation.Always, DisplayName = "--delegation always")]
    [DataRow(new[] { "--delegation", "none" }, null, null, SecurityDelegation.None, DisplayName = "--delegation none")]
    public void NegotiateOptionsMapping_EachOption_ReachesTheNegotiateOptions(string[] arguments, string? serviceName, string? proxyServiceName, SecurityDelegation delegation)
    {
        Diagnostics.ArrangeCommandLine([.. arguments, Url]);
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, Url], _ => true);
        Assert.IsNotNull(parsed.Options);

        NegotiateOptions options = NegotiateOptionsMapping.FromCommandLine(parsed.Options);
        Diagnostics.Act("Negotiate options", options);

        NegotiateOptions expected = new(serviceName, proxyServiceName, delegation);
        Diagnostics.Assert("Negotiate options", expected, options);
        Assert.AreEqual(expected, options);
    }

    [TestMethod]
    public async Task RunAsync_AnyauthOfferedNegotiate_AnswersTheChallengeWithTheToken()
    {
        TokenSource tokens = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x60, 0x01, 0x00]));
        ScriptedConnector server = new(
        [
            Encoding.Latin1.GetBytes(Unauthorized.Replace("Content-Length: 4\r\n", "Content-Length: 4\r\nConnection: close\r\n", StringComparison.Ordinal)),
            Encoding.Latin1.GetBytes(Ok),
        ]);

        (int exitCode, string standardOutput) = await RunAsync(server, tokens, "--anyauth", "-u", "alice:pw", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Request + Request.Replace("User-Agent", "Authorization: Negotiate YAEA\r\nUser-Agent", StringComparison.Ordinal), Latin1(server.Written));
        Assert.AreEqual("hello", standardOutput);
        Assert.AreEqual("alice", tokens.Requests.Single().UserName);
    }

    [TestMethod]
    public async Task RunAsync_NegotiateWithoutATicket_SendsOneRequestAndWritesThe401()
    {
        TokenSource tokens = new(new SecurityContextStep(SecurityContextStatus.NoCredentials, []));
        ScriptedConnector server = new([Encoding.Latin1.GetBytes(Unauthorized)]);

        (int exitCode, string standardOutput) = await RunAsync(server, tokens, "--negotiate", "-u", ":", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Request, Latin1(server.Written));
        Assert.AreEqual("deny", standardOutput);
        Assert.HasCount(2, tokens.Requests, "Tried before the first request and again for the 401, as curl does.");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task RunAsync_WindowsNegotiateWithoutADomain_SendsOneRequestAndExitsZeroAsCurl8210Measured()
    {
        await AssertProductionNegotiateSendsNothingAsync();
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task RunAsync_NegotiateOffWindowsWithoutATicket_SendsOneRequestAndExitsZeroAsCurl8180Measured()
    {
        await AssertProductionNegotiateSendsNothingAsync();
    }

    private async Task AssertProductionNegotiateSendsNothingAsync()
    {
        ScriptedConnector server = new([Encoding.Latin1.GetBytes(Unauthorized)]);

        (int exitCode, string standardOutput) = await RunAsync(server, null, "--negotiate", "-u", ":", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Request, Latin1(server.Written));
        Assert.AreEqual("deny", standardOutput);
    }

    private async Task<(int ExitCode, string StandardOutput)> RunAsync(ScriptedConnector server, ISecurityContextFactory? tokens, params string[] arguments)
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

        string output = Encoding.Latin1.GetString(standardOutput.ToArray());
        Diagnostics.ActRun(exitCode, output, Encoding.UTF8.GetString(standardError.ToArray()));
        Diagnostics.ActWritten(server);
        return (exitCode, output);
    }

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    /// <summary>Hands out a context whose one step is <paramref name="step" /> for every request, and records the requests.</summary>
    private sealed class TokenSource(SecurityContextStep step) : ISecurityContextFactory
    {
        public List<SecurityContextRequest> Requests { get; } = [];

        public ISecurityContext Create(SecurityContextRequest request)
        {
            Requests.Add(request);
            return new OneStepContext(step);
        }
    }

    private sealed class OneStepContext(SecurityContextStep step) : ISecurityContext
    {
        public bool IsCompleted => false;

        public ValueTask<SecurityContextStep> NextTokenAsync(ReadOnlyMemory<byte> incomingToken, CancellationToken cancellationToken) =>
            ValueTask.FromResult(step);

        public byte[]? Wrap(ReadOnlySpan<byte> message, bool encrypt) => throw new NotSupportedException();

        public byte[]? Unwrap(ReadOnlySpan<byte> wrappedMessage) => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }
}
