using Curl.Core.Fakes;
using Curl.Core.Hsts;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Core;

/// <summary>
/// Pins how <see cref="RedirectFollower" /> uses the run's HSTS cache, as curl 8.21.0 does (measured
/// 2026-09-29, BL-621 Notes): every hop's <c>https</c> response is learned from, with or without
/// <c>-L</c>, by the HTTP handler through the hop's <see cref="HttpRequestOptions.HstsStore" />
/// (ADR-0409; the scripted handler here does it as that handler does), and an <c>http</c> redirect target the cache then knows is switched to <c>https</c>,
/// reported, and checked against <c>--proto-redir</c> as switched.
/// </summary>
[TestClass]
public sealed class RedirectFollowerHstsTests
{
    private const string Start = "https://localhost:18443/";

    private const string HttpTarget = "http://localhost:18443/x";

    private static readonly DateTimeOffset Now = new(2026, 9, 29, 14, 22, 10, TimeSpan.Zero);

    private readonly HstsTransferPolicy hsts = new(new FakeTimeProvider(Now));

    private readonly RecordingTransferEvents events = new();

    [TestMethod]
    public async Task FollowAsync_HttpTargetOfAHostTheHopTaught_IsSwitchedReportedAndCountedAsFollowed()
    {
        ScriptedHandler handler = new(Response(301, HttpTarget, "max-age=60"), Response(200));

        TransferResult result = await FollowAsync(handler, Start, new RedirectPolicy());

        Assert.AreEqual("https://localhost:18443/x", handler.Urls[1].OriginalString);
        Assert.AreEqual("https://localhost:18443/x", result.Report!.EffectiveUrl);
        Assert.AreEqual(1, result.Report.RedirectCount);
        CollectionAssert.AreEqual(
            new[] { RedirectFollower.IssueAnotherRequestMessagePrefix + HttpTarget + "'", HstsTransferPolicy.SwitchedMessagePrefix + "https://localhost:18443/x" },
            events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_HttpTargetOfAnUnknownHost_IsFollowedAsItIsWithoutASwitchLine()
    {
        ScriptedHandler handler = new(Response(301, HttpTarget), Response(200));

        await FollowAsync(handler, Start, new RedirectPolicy());

        Assert.AreEqual(HttpTarget, handler.Urls[1].OriginalString);
        CollectionAssert.AreEqual(new[] { RedirectFollower.IssueAnotherRequestMessagePrefix + HttpTarget + "'" }, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_SwitchedTarget_IsCheckedAgainstProtoRedirAsHttps()
    {
        ScriptedHandler handler = new(Response(301, HttpTarget, "max-age=60"), Response(200));

        TransferResult result = await FollowAsync(handler, Start, new RedirectPolicy { AllowedSchemes = new HashSet<string> { "http" } });

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual("Protocol \"https\" is disabled (in redirect)", result.ErrorMessage);
        Assert.HasCount(1, handler.Urls);
    }

    [TestMethod]
    public async Task FollowAsync_UnparsableTarget_IsRefusedWithoutASwitch()
    {
        ScriptedHandler handler = new(Response(301, "http://local host/", "max-age=60"));

        TransferResult result = await FollowAsync(handler, Start, new RedirectPolicy());

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.IsEmpty(events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_WithoutLocation_StillLearnsFromTheResponse()
    {
        ScriptedHandler handler = new(Response(200, header: "max-age=60"));

        await new RedirectFollower(new ProtocolDispatcher([handler]), hsts: hsts)
            .FollowAsync(Context(Start, new HttpRequestOptions()), new RedirectPolicy());

        Assert.IsTrue(hsts.TrySwitchToHttps("http://localhost/", CurlUrl.Parse("http://localhost/"), out _));
    }

    [TestMethod]
    public async Task FollowAsync_WithoutACache_FollowsAsBefore()
    {
        ScriptedHandler handler = new(Response(301, HttpTarget, "max-age=60"), Response(200));

        await new RedirectFollower(new ProtocolDispatcher([handler]))
            .FollowAsync(Context(Start, new HttpRequestOptions { FollowRedirects = true }), new RedirectPolicy());

        Assert.AreEqual(HttpTarget, handler.Urls[1].OriginalString);
    }

    private Task<TransferResult> FollowAsync(ScriptedHandler handler, string url, RedirectPolicy policy) =>
        new RedirectFollower(new ProtocolDispatcher([handler]), hsts: hsts)
            .FollowAsync(Context(url, new HttpRequestOptions { FollowRedirects = true }), policy)
            .AsTask();

    private TransferContext Context(string url, HttpRequestOptions http) =>
        new()
        {
            Url = CurlUrl.Parse(url),
            Output = Stream.Null,
            Http = http with { HstsStore = hsts },
            TimeProvider = TimeProvider.System,
            Events = events,
        };

    private static TransferResult Response(int status, string? location = null, string? header = null) =>
        TransferResult.Success(0) with
        {
            Report = new TransferReport
            {
                ResponseCode = status,
                RedirectUrl = location,
                ResponseHeaders = header is null ? [] : [new("Strict-Transport-Security", header)],
            },
        };

    /// <summary>
    /// Serves http and https, answering each call with the next scripted result after handing its
    /// <c>Strict-Transport-Security</c> header to the hop's <see cref="HttpRequestOptions.HstsStore" />, as the HTTP handler does.
    /// </summary>
    private sealed class ScriptedHandler(params TransferResult[] script) : IProtocolHandler
    {
        public List<CurlUrl> Urls { get; } = [];

        public IReadOnlyCollection<string> SupportedSchemes => ["http", "https"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            Urls.Add(context.Url);
            TransferResult result = script[Urls.Count - 1];
            foreach (KeyValuePair<string, string> header in result.Report!.ResponseHeaders)
            {
                context.Http!.HstsStore?.StoreFromResponse(context.Url, header.Value, Now);
            }

            return ValueTask.FromResult(result);
        }
    }
}
