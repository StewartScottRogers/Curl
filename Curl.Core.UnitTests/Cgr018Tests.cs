using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Core;

/// <summary>
/// Pins CGR-018 of the 2026-10-25 gap report: a redirect target that starts with scheme-like
/// text and a colon but no <c>//</c>, such as <c>localhost:20200/x</c>, names that scheme, as
/// curl 8.22.0 reads it, so <see cref="RedirectFollower" /> refuses it with exit 1,
/// <c>The redirect target URL could not be parsed: Unsupported URL scheme</c>, and sends no
/// second request, where it used to guess <c>http</c> and follow it to another host.
/// </summary>
[TestClass]
public sealed class Cgr018Tests
{
    private const string Start = "http://127.0.0.1:20200/__loc=localhost:20200/x";

    private const string UnsupportedSchemeMessage = "The redirect target URL could not be parsed: Unsupported URL scheme";

    [TestMethod]
    [DataRow("localhost:20200/x", false)]
    [DataRow("Localhost:20200/x", true)]
    [DataRow("foo:bar", false)]
    [DataRow("c:/x", false)]
    public async Task FollowAsync_TargetNamingASchemeCurlDoesNotKnowWithoutSlashes_Exits1AndSendsNoSecondRequest(string target, bool runsOnWindows)
    {
        // curl 8.22.0 -sS -L, Location: localhost:20200/x -> exit 1, "curl: (1) The redirect
        // target URL could not be parsed: Unsupported URL scheme", one request (BL-3547 Notes).
        ScriptedHandler handler = new(Response(302, target), Response(200));

        TransferResult result = await FollowAsync(handler, runsOnWindows);

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual(UnsupportedSchemeMessage, result.ErrorMessage);
        Assert.AreEqual(1, handler.Calls);
    }

    [TestMethod]
    [DataRow("http:/127.0.0.1:20200/y")]
    [DataRow("HTTP://127.0.0.1:20200/y")]
    public async Task FollowAsync_TargetNamingAKnownScheme_IsFollowed(string target)
    {
        // curl 8.22.0 -sS -L, Location: http:/127.0.0.1:18731/y -> exit 0, GET /y (BL-3547 Notes).
        ScriptedHandler handler = new(Response(302, target), Response(200));

        TransferResult result = await FollowAsync(handler, runsOnWindows: false);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(2, handler.Calls);
    }

    [TestMethod]
    [DataRow("/x:y", false)]
    [DataRow("a_b:x", false)]
    [DataRow("x", false)]
    public async Task FollowAsync_TargetNamingNoScheme_IsNotRefusedAsAnUnsupportedScheme(string target, bool runsOnWindows)
    {
        ScriptedHandler handler = new(Response(302, target), Response(200));

        TransferResult result = await FollowAsync(handler, runsOnWindows);

        Assert.AreNotEqual(UnsupportedSchemeMessage, result.ErrorMessage);
    }

    private static Task<TransferResult> FollowAsync(ScriptedHandler handler, bool runsOnWindows) =>
        new RedirectFollower(new ProtocolDispatcher([handler]), runsOnWindows: runsOnWindows)
            .FollowAsync(
                new TransferContext
                {
                    Url = CurlUrl.Parse(Start),
                    Output = Stream.Null,
                    Http = new HttpRequestOptions { FollowRedirects = true },
                    TimeProvider = TimeProvider.System,
                },
                new RedirectPolicy())
            .AsTask();

    private static TransferResult Response(int status, string? location = null) =>
        TransferResult.Success(0) with { Report = new TransferReport { ResponseCode = status, RedirectUrl = location } };

    /// <summary>Serves http, answering each call with the next scripted result.</summary>
    private sealed class ScriptedHandler(params TransferResult[] script) : IProtocolHandler
    {
        public int Calls { get; private set; }

        public IReadOnlyCollection<string> SupportedSchemes => ["http"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context) =>
            ValueTask.FromResult(script[Calls++]);
    }
}
