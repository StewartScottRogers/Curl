using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;
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

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task FollowAsync_FollowWithCustomPostOn303_ReportsSwitchToGet()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        await FollowAsync(303, new HttpRequestOptions { FollowRedirects = true, CustomMethod = "POST" }, FollowPolicy());

        var expected = new[] { IssueLine, "Switch to GET because of 303 response" };
        diagnostics.Assert("infos", string.Join(" | ", expected), string.Join(" | ", events.Infos));
        CollectionAssert.AreEqual(expected, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_FollowWithPostBodyOn302_ReportsSwitchToGet()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        await FollowAsync(302, new HttpRequestOptions { FollowRedirects = true, Body = PostBody }, FollowPolicy());

        var expected = new[] { IssueLine, "Switch to GET because of 302 response" };
        diagnostics.Assert("infos", string.Join(" | ", expected), string.Join(" | ", events.Infos));
        CollectionAssert.AreEqual(expected, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_FollowWithUploadOn303_ReportsSwitchToGet()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        await FollowAsync(303, new HttpRequestOptions { FollowRedirects = true }, FollowPolicy(), upload: true);

        var expected = new[] { IssueLine, "Switch to GET because of 303 response" };
        diagnostics.Assert("infos", string.Join(" | ", expected), string.Join(" | ", events.Infos));
        CollectionAssert.AreEqual(expected, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_LocationWithCustomPostOn303_ReportsStickToPost()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        await FollowAsync(303, new HttpRequestOptions { FollowRedirects = true, CustomMethod = "POST" }, new RedirectPolicy());

        var expected = new[] { IssueLine, "Stick to POST instead of GET" };
        diagnostics.Assert("infos", string.Join(" | ", expected), string.Join(" | ", events.Infos));
        CollectionAssert.AreEqual(expected, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_LocationWithCustomPostAndBodyOn303_ReportsStickToPost()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        await FollowAsync(303, new HttpRequestOptions { FollowRedirects = true, CustomMethod = "POST", Body = PostBody }, new RedirectPolicy());

        var expected = new[] { IssueLine, "Stick to POST instead of GET" };
        diagnostics.Assert("infos", string.Join(" | ", expected), string.Join(" | ", events.Infos));
        CollectionAssert.AreEqual(expected, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_LocationWithPostBodyOn303_ReportsNoSwitchLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        await FollowAsync(303, new HttpRequestOptions { FollowRedirects = true, Body = PostBody }, new RedirectPolicy());

        var expected = new[] { IssueLine };
        diagnostics.Assert("infos", string.Join(" | ", expected), string.Join(" | ", events.Infos));
        CollectionAssert.AreEqual(expected, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_LocationWithCustomPutOn302_ReportsNoSwitchLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        await FollowAsync(302, new HttpRequestOptions { FollowRedirects = true, CustomMethod = "PUT" }, new RedirectPolicy());

        var expected = new[] { IssueLine };
        diagnostics.Assert("infos", string.Join(" | ", expected), string.Join(" | ", events.Infos));
        CollectionAssert.AreEqual(expected, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_PostUnderPost302On302_ReportsNoSwitchLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        await FollowAsync(302, new HttpRequestOptions { FollowRedirects = true, Body = PostBody }, FollowPolicy() with { KeepPostOn302 = true });

        var expected = new[] { IssueLine };
        diagnostics.Assert("infos", string.Join(" | ", expected), string.Join(" | ", events.Infos));
        CollectionAssert.AreEqual(expected, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_PostUnderPost301On301_ReportsNoSwitchLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        await FollowAsync(301, new HttpRequestOptions { FollowRedirects = true, Body = PostBody }, FollowPolicy() with { KeepPostOn301 = true });

        var expected = new[] { IssueLine };
        diagnostics.Assert("infos", string.Join(" | ", expected), string.Join(" | ", events.Infos));
        CollectionAssert.AreEqual(expected, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_PostUnderPost303On303_ReportsNoSwitchLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        await FollowAsync(303, new HttpRequestOptions { FollowRedirects = true, CustomMethod = "POST", Body = PostBody }, FollowPolicy() with { KeepPostOn303 = true });

        var expected = new[] { IssueLine };
        diagnostics.Assert("infos", string.Join(" | ", expected), string.Join(" | ", events.Infos));
        CollectionAssert.AreEqual(expected, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_GetOn301_ReportsNoSwitchLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        await FollowAsync(301, new HttpRequestOptions { FollowRedirects = true }, FollowPolicy());

        var expected = new[] { IssueLine };
        diagnostics.Assert("infos", string.Join(" | ", expected), string.Join(" | ", events.Infos));
        CollectionAssert.AreEqual(expected, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_GetOn303_ReportsNoSwitchLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        await FollowAsync(303, new HttpRequestOptions { FollowRedirects = true }, FollowPolicy());

        var expected = new[] { IssueLine };
        diagnostics.Assert("infos", string.Join(" | ", expected), string.Join(" | ", events.Infos));
        CollectionAssert.AreEqual(expected, events.Infos);
    }

    [TestMethod]
    public async Task FollowAsync_CustomPostOn307_ReportsNoSwitchLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        await FollowAsync(307, new HttpRequestOptions { FollowRedirects = true, CustomMethod = "POST", Body = PostBody }, FollowPolicy());

        var expected = new[] { IssueLine };
        diagnostics.Assert("infos", string.Join(" | ", expected), string.Join(" | ", events.Infos));
        CollectionAssert.AreEqual(expected, events.Infos);
    }

    private static string IssueLine => RedirectFollower.IssueAnotherRequestMessagePrefix + Target + "'";

    private static RedirectPolicy FollowPolicy() => new() { DropsCustomMethodOnSwitchToGet = true };

    private async Task FollowAsync(int status, HttpRequestOptions http, RedirectPolicy policy, bool upload = false)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedHandler handler = new(Response(status, Target), Response(200));
        diagnostics.Arrange("start url", Start);
        diagnostics.Arrange("script", status + " to " + Target + " | 200");
        diagnostics.Arrange("custom method", http.CustomMethod ?? "(none)");
        diagnostics.Arrange("has body", http.Body is not null);
        diagnostics.Arrange("upload", upload);

        TransferResult result;
        using (diagnostics.Phase("follow"))
        {
            result = await new RedirectFollower(new ProtocolDispatcher([handler]))
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
        }

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("infos", string.Join(" | ", events.Infos));
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
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
