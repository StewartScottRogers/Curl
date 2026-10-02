using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;
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

    [TestMethod]
    public async Task FollowAsync_FollowedRedirect_ReportsTheLineBeforeRequestingTheTarget()
    {
        int infosWhenTargetRequested = -1;
        ScriptedHandler handler = new(Response(301, Target), Response(200)) { OnRequest = () => infosWhenTargetRequested = events.Infos.Count };

        TransferResult result = await FollowAsync(handler, new RedirectPolicy());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { IssueLine(Target) }, events.Infos);
        Assert.AreEqual(1, infosWhenTargetRequested);
    }

    [TestMethod]
    public async Task FollowAsync_TwoFollowedRedirects_ReportsTheLineForEach()
    {
        ScriptedHandler handler = new(Response(302, Target), Response(301, Start + "y"), Response(200));

        await FollowAsync(handler, new RedirectPolicy());

        CollectionAssert.AreEqual(new[] { IssueLine(Target), IssueLine(Start + "y") }, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_MaxRedirsRefusal_ReportsNoLine()
    {
        ScriptedHandler handler = new(Response(301, Target));

        TransferResult result = await FollowAsync(handler, new RedirectPolicy { MaxRedirects = 0 });

        Assert.AreEqual(CurlExitCode.TooManyRedirects, result.ExitCode);
        Assert.IsEmpty(events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_UnparsableTarget_ReportsNoLine()
    {
        ScriptedHandler handler = new(Response(301, "http://[::1/x"));

        TransferResult result = await FollowAsync(handler, new RedirectPolicy());

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.IsEmpty(events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_TargetWithAnUnknownScheme_ReportsNoLine()
    {
        ScriptedHandler handler = new(Response(301, "foo://127.0.0.1/x"));

        TransferResult result = await FollowAsync(handler, new RedirectPolicy());

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual("The redirect target URL could not be parsed: Unsupported URL scheme", result.ErrorMessage);
        Assert.IsEmpty(events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_ProtoRedirRefusal_ReportsTheLineFirst()
    {
        ScriptedHandler handler = new(Response(301, Target));

        TransferResult result = await FollowAsync(handler, new RedirectPolicy { AllowedSchemes = new HashSet<string> { "https" } });

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual("Protocol \"http\" is disabled (in redirect)", result.ErrorMessage);
        // curl 8.21.0's -v also writes the refusal as an info line (measured 2026-10-01, BL-805 Notes).
        CollectionAssert.AreEqual(new[] { IssueLine(Target), "Protocol \"http\" is disabled (in redirect)" }, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_UserInUrlRefusal_ReportsTheLineWithTheCredentials()
    {
        ScriptedHandler handler = new(Response(301, "http://u:p@127.0.0.1:18907/x"));

        TransferResult result = await FollowAsync(handler, new RedirectPolicy { DisallowsUserInUrl = true });

        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        CollectionAssert.AreEqual(new[] { IssueLine("http://u:p@127.0.0.1:18907/x") }, events.Infos);
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
