using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Core;

/// <summary>
/// Pins when <see cref="RedirectFollower" /> reports curl 8.21.0's <c>http_switch_to_get</c> lines
/// right after <c>Issue another request to this URL: '...'</c> (measured 2026-10-03, BL-1352):
/// <c>Switch to GET because of N response</c> under <c>--follow</c> and
/// <c>Stick to M instead of GET</c> under <c>-L</c> with <c>-X</c>.
/// </summary>
[TestClass]
public sealed class RedirectFollowerMethodSwitchLineTests
{
    private const string Start = "http://127.0.0.1:18907/";

    private const string Target = "http://127.0.0.1:18907/b";

    private static readonly BytesBody PostBody = new("x"u8.ToArray(), "application/x-www-form-urlencoded");

    private readonly RecordingTransferEvents events = new();

    [TestMethod]
    public async Task FollowAsync_FollowWithCustomPostOn303_ReportsSwitchToGet()
    {
        await FollowAsync(303, new HttpRequestOptions { FollowRedirects = true, CustomMethod = "POST" }, FollowPolicy());

        CollectionAssert.AreEqual(new[] { IssueLine, "Switch to GET because of 303 response" }, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_FollowWithPostBodyOn302_ReportsSwitchToGet()
    {
        await FollowAsync(302, new HttpRequestOptions { FollowRedirects = true, Body = PostBody }, FollowPolicy());

        CollectionAssert.AreEqual(new[] { IssueLine, "Switch to GET because of 302 response" }, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_FollowWithUploadOn303_ReportsSwitchToGet()
    {
        await FollowAsync(303, new HttpRequestOptions { FollowRedirects = true }, FollowPolicy(), upload: true);

        CollectionAssert.AreEqual(new[] { IssueLine, "Switch to GET because of 303 response" }, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_LocationWithCustomPostOn303_ReportsStickToPost()
    {
        await FollowAsync(303, new HttpRequestOptions { FollowRedirects = true, CustomMethod = "POST" }, new RedirectPolicy());

        CollectionAssert.AreEqual(new[] { IssueLine, "Stick to POST instead of GET" }, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_LocationWithCustomPostAndBodyOn303_ReportsStickToPost()
    {
        await FollowAsync(303, new HttpRequestOptions { FollowRedirects = true, CustomMethod = "POST", Body = PostBody }, new RedirectPolicy());

        CollectionAssert.AreEqual(new[] { IssueLine, "Stick to POST instead of GET" }, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_LocationWithPostBodyOn303_ReportsNoSwitchLine()
    {
        await FollowAsync(303, new HttpRequestOptions { FollowRedirects = true, Body = PostBody }, new RedirectPolicy());

        CollectionAssert.AreEqual(new[] { IssueLine }, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_LocationWithCustomPutOn302_ReportsNoSwitchLine()
    {
        await FollowAsync(302, new HttpRequestOptions { FollowRedirects = true, CustomMethod = "PUT" }, new RedirectPolicy());

        CollectionAssert.AreEqual(new[] { IssueLine }, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_PostUnderPost302On302_ReportsNoSwitchLine()
    {
        await FollowAsync(302, new HttpRequestOptions { FollowRedirects = true, Body = PostBody }, FollowPolicy() with { KeepPostOn302 = true });

        CollectionAssert.AreEqual(new[] { IssueLine }, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_PostUnderPost301On301_ReportsNoSwitchLine()
    {
        await FollowAsync(301, new HttpRequestOptions { FollowRedirects = true, Body = PostBody }, FollowPolicy() with { KeepPostOn301 = true });

        CollectionAssert.AreEqual(new[] { IssueLine }, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_PostUnderPost303On303_ReportsNoSwitchLine()
    {
        await FollowAsync(303, new HttpRequestOptions { FollowRedirects = true, CustomMethod = "POST", Body = PostBody }, FollowPolicy() with { KeepPostOn303 = true });

        CollectionAssert.AreEqual(new[] { IssueLine }, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_GetOn301_ReportsNoSwitchLine()
    {
        await FollowAsync(301, new HttpRequestOptions { FollowRedirects = true }, FollowPolicy());

        CollectionAssert.AreEqual(new[] { IssueLine }, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_GetOn303_ReportsNoSwitchLine()
    {
        await FollowAsync(303, new HttpRequestOptions { FollowRedirects = true }, FollowPolicy());

        CollectionAssert.AreEqual(new[] { IssueLine }, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_CustomPostOn307_ReportsNoSwitchLine()
    {
        await FollowAsync(307, new HttpRequestOptions { FollowRedirects = true, CustomMethod = "POST", Body = PostBody }, FollowPolicy());

        CollectionAssert.AreEqual(new[] { IssueLine }, events.Infos);
    }

    private static string IssueLine => RedirectFollower.IssueAnotherRequestMessagePrefix + Target + "'";

    private static RedirectPolicy FollowPolicy() => new() { DropsCustomMethodOnSwitchToGet = true };

    private async Task FollowAsync(int status, HttpRequestOptions http, RedirectPolicy policy, bool upload = false)
    {
        ScriptedHandler handler = new(Response(status, Target), Response(200));
        TransferResult result = await new RedirectFollower(new ProtocolDispatcher([handler]))
            .FollowAsync(
                new TransferContext
                {
                    Url = CurlUrl.Parse(Start),
                    Output = Stream.Null,
                    Http = http,
                    Upload = upload ? new MemoryStream([1, 2, 3]) : null,
                    TimeProvider = TimeProvider.System,
                    Events = events,
                },
                policy);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    private static TransferResult Response(int status, string? location = null) =>
        TransferResult.Success(0) with { Report = new TransferReport { ResponseCode = status, RedirectUrl = location } };

    /// <summary>Serves http, answering each call with the next scripted result.</summary>
    private sealed class ScriptedHandler(params TransferResult[] script) : IProtocolHandler
    {
        private int calls;

        public IReadOnlyCollection<string> SupportedSchemes => ["http"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context) =>
            ValueTask.FromResult(script[calls++]);
    }
}
