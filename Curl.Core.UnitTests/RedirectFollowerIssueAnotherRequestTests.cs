using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Core;

/// <summary>
/// Pins when <see cref="RedirectFollower" /> reports curl 8.21.0's
/// <c>Issue another request to this URL: '...'</c> line under <c>-L</c> (measured 2026-09-30, BL-907
/// Notes): once for each target that parses with a scheme curl knows, before the hop is requested
/// and before the <c>--proto-redir</c> and <c>--disallow-username-in-url</c> refusals, and never
/// when <c>--max-redirs</c> refuses the hop or the target does not parse.
/// </summary>
[TestClass]
public sealed class RedirectFollowerIssueAnotherRequestTests
{
    private const string Start = "http://127.0.0.1:18907/";

    private const string Target = "http://127.0.0.1:18907/x";

    private readonly RecordingTransferEvents events = new();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task FollowAsync_FollowedRedirect_ReportsTheLineBeforeRequestingTheTarget()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        int infosWhenTargetRequested = -1;
        ScriptedHandler handler = new(Response(301, Target), Response(200)) { OnRequest = () => infosWhenTargetRequested = events.Infos.Count };
        diagnostics.Arrange("start url", Start);
        diagnostics.Arrange("script", "301 to " + Target + " | 200");

        TransferResult result;
        using (diagnostics.Phase("follow"))
        {
            result = await FollowAsync(handler, new RedirectPolicy());
        }

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("infos", string.Join(" | ", events.Infos));
        diagnostics.Act("infos when target requested", infosWhenTargetRequested);
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { IssueLine(Target) }, events.Infos);
        diagnostics.Assert("infos when target requested", 1, infosWhenTargetRequested);
        Assert.AreEqual(1, infosWhenTargetRequested);
    }

    [TestMethod]
    public async Task FollowAsync_TwoFollowedRedirects_ReportsTheLineForEach()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedHandler handler = new(Response(302, Target), Response(301, Start + "y"), Response(200));
        diagnostics.Arrange("start url", Start);
        diagnostics.Arrange("script", "302 to " + Target + " | 301 to " + Start + "y | 200");

        using (diagnostics.Phase("follow"))
        {
            await FollowAsync(handler, new RedirectPolicy());
        }

        var expected = new[] { IssueLine(Target), IssueLine(Start + "y") };
        diagnostics.Act("infos", string.Join(" | ", events.Infos));
        diagnostics.Assert("infos", string.Join(" | ", expected), string.Join(" | ", events.Infos));
        CollectionAssert.AreEqual(expected, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_MaxRedirsRefusal_ReportsNoLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedHandler handler = new(Response(301, Target));
        diagnostics.Arrange("start url", Start);
        diagnostics.Arrange("policy", "max redirects 0");

        TransferResult result;
        using (diagnostics.Phase("follow"))
        {
            result = await FollowAsync(handler, new RedirectPolicy { MaxRedirects = 0 });
        }

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("infos", string.Join(" | ", events.Infos));
        diagnostics.Assert("exit code", CurlExitCode.TooManyRedirects, result.ExitCode);
        Assert.AreEqual(CurlExitCode.TooManyRedirects, result.ExitCode);
        Assert.IsEmpty(events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_UnparsableTarget_ReportsNoLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedHandler handler = new(Response(301, "http://[::1/x"));
        diagnostics.Arrange("start url", Start);
        diagnostics.Arrange("script", "301 to http://[::1/x");

        TransferResult result;
        using (diagnostics.Phase("follow"))
        {
            result = await FollowAsync(handler, new RedirectPolicy());
        }

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("infos", string.Join(" | ", events.Infos));
        diagnostics.Assert("exit code", CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.IsEmpty(events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_TargetWithAnUnknownScheme_ReportsNoLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedHandler handler = new(Response(301, "foo://127.0.0.1/x"));
        diagnostics.Arrange("start url", Start);
        diagnostics.Arrange("script", "301 to foo://127.0.0.1/x");

        TransferResult result;
        using (diagnostics.Phase("follow"))
        {
            result = await FollowAsync(handler, new RedirectPolicy());
        }

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Act("infos", string.Join(" | ", events.Infos));
        diagnostics.Assert("exit code", CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual("The redirect target URL could not be parsed: Unsupported URL scheme", result.ErrorMessage);
        Assert.IsEmpty(events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_ProtoRedirRefusal_ReportsTheLineFirst()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedHandler handler = new(Response(301, Target));
        diagnostics.Arrange("start url", Start);
        diagnostics.Arrange("policy", "allowed schemes: https");

        TransferResult result;
        using (diagnostics.Phase("follow"))
        {
            result = await FollowAsync(handler, new RedirectPolicy { AllowedSchemes = new HashSet<string> { "https" } });
        }

        var expected = new[] { IssueLine(Target), "Protocol \"http\" is disabled (in redirect)" };
        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Act("infos", string.Join(" | ", events.Infos));
        diagnostics.Assert("exit code", CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual("Protocol \"http\" is disabled (in redirect)", result.ErrorMessage);
        // curl 8.21.0's -v also writes the refusal as an info line (measured 2026-10-01, BL-805 Notes).
        diagnostics.Assert("infos", string.Join(" | ", expected), string.Join(" | ", events.Infos));
        CollectionAssert.AreEqual(expected, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_UserInUrlRefusal_ReportsTheLineWithTheCredentials()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedHandler handler = new(Response(301, "http://u:p@127.0.0.1:18907/x"));
        diagnostics.Arrange("start url", Start);
        diagnostics.Arrange("policy", "disallows user in url");

        TransferResult result;
        using (diagnostics.Phase("follow"))
        {
            result = await FollowAsync(handler, new RedirectPolicy { DisallowsUserInUrl = true });
        }

        var expected = new[] { IssueLine("http://u:p@127.0.0.1:18907/x") };
        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("infos", string.Join(" | ", events.Infos));
        diagnostics.Assert("exit code", CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        diagnostics.Assert("infos", string.Join(" | ", expected), string.Join(" | ", events.Infos));
        CollectionAssert.AreEqual(expected, events.Infos);
    }

    private static string IssueLine(string url) => RedirectFollower.IssueAnotherRequestMessagePrefix + url + "'";

    private Task<TransferResult> FollowAsync(ScriptedHandler handler, RedirectPolicy policy) =>
        new RedirectFollower(new ProtocolDispatcher([handler]))
            .FollowAsync(
                new TransferContext
                {
                    Url = CurlUrl.Parse(Start),
                    Output = Stream.Null,
                    Http = new HttpRequestOptions { FollowRedirects = true },
                    TimeProvider = TimeProvider.System,
                    Events = events,
                },
                policy)
            .AsTask();

    private static TransferResult Response(int status, string? location = null) =>
        TransferResult.Success(0) with { Report = new TransferReport { ResponseCode = status, RedirectUrl = location } };

    /// <summary>Serves http, answering each call with the next scripted result.</summary>
    private sealed class ScriptedHandler(params TransferResult[] script) : IProtocolHandler
    {
        private int calls;

        public Action? OnRequest { get; init; }

        public IReadOnlyCollection<string> SupportedSchemes => ["http"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            if (calls > 0)
            {
                OnRequest?.Invoke();
            }

            return ValueTask.FromResult(script[calls++]);
        }
    }
}
