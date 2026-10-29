using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Core;

/// <summary>
/// Pins CGR-062 part 2 (BL-3867): a redirect target whose header bytes the HTTP handler marked
/// (<see cref="HeaderByteText" />, ADR-0923) is followed as its Latin-1 text, and the effective
/// URL it becomes stays marked, so <c>%{url_effective}</c> writes those bytes back as curl 8.22.0
/// Schannel does.
/// </summary>
[TestClass]
public sealed class Cgr062RedirectFollowerTests
{
    private const string Start = "http://127.0.0.1:18867/a/";

    [TestMethod]
    public async Task FollowAsync_MarkedTarget_FollowsItsTextAndKeepsTheEffectiveUrlMarked()
    {
        string target = HeaderByteText.Mark("http://x.test/é");
        ScriptedHandler handler = new(Response(302, target), Response(200));

        TransferResult result = await FollowAsync(handler);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("/%E9", handler.LastUrl!.AbsolutePath);
        Assert.AreEqual(target, result.Report!.EffectiveUrl);
    }

    [TestMethod]
    public async Task FollowAsync_MarkedTargetRefusedByScheme_KeepsTheEffectiveUrlMarked()
    {
        string target = HeaderByteText.Mark("dict://x.test/é");
        ScriptedHandler handler = new(Response(302, target));

        TransferResult result = await FollowAsync(handler);

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual(target, result.Report!.EffectiveUrl);
    }

    [TestMethod]
    public async Task FollowAsync_UnmarkedTarget_KeepsTheEffectiveUrlAsText()
    {
        ScriptedHandler handler = new(Response(302, "http://å.se/b"), Response(200));

        TransferResult result = await FollowAsync(handler);

        Assert.AreEqual("http://å.se/b", result.Report!.EffectiveUrl);
    }

    private static Task<TransferResult> FollowAsync(ScriptedHandler handler) =>
        new RedirectFollower(new ProtocolDispatcher([handler]))
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
        private int calls;

        public CurlUrl? LastUrl { get; private set; }

        public IReadOnlyCollection<string> SupportedSchemes => ["http"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            LastUrl = context.Url;
            return ValueTask.FromResult(script[calls++]);
        }
    }
}
