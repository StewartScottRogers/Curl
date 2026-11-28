using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Core;

/// <summary>
/// Pins what <see cref="RedirectFollower" /> reports for the response when a followed hop fails
/// before any response arrives: curl 8.21.0 keeps the followed response's <c>%{http_code}</c>,
/// <c>%{http_version}</c> and <c>%{content_type}</c>, both when <c>-m</c> ends the hop and when
/// its connection is refused (measured 2026-11-28, BL-6095 Notes).
/// </summary>
[TestClass]
public sealed class RedirectFollowerTimedOutHopResponseCodeTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task FollowAsync_MaxTimeEndsAFollowedHopWithNoResponse_KeepsTheFollowedResponse()
    {
        TransferReport report = await ReportAsync(Redirect(302), TimedOut(new TransferReport()));

        AssertResponse(report, 302, System.Net.HttpVersion.Version11, "text/x-first");
        Assert.AreEqual(0, report.ResponseHeaders.Count);
    }

    [TestMethod]
    public async Task FollowAsync_FollowedHopFailsWithNoReport_KeepsTheFollowedResponse()
    {
        TransferReport report = await ReportAsync(
            Redirect(302),
            TransferResult.Failure(CurlExitCode.CouldntConnect, "Could not connect to server", 0));

        AssertResponse(report, 302, System.Net.HttpVersion.Version11, "text/x-first");
    }

    [TestMethod]
    public async Task FollowAsync_ThirdHopHasNoResponse_KeepsTheSecondHopsResponse()
    {
        TransferReport report = await ReportAsync(
            Redirect(301),
            Redirect(307, System.Net.HttpVersion.Version10, "text/x-second"),
            TimedOut(new TransferReport()));

        AssertResponse(report, 307, System.Net.HttpVersion.Version10, "text/x-second");
    }

    [TestMethod]
    public async Task FollowAsync_FailedHopAlreadyReportsACodeAndContentType_KeepsThemAndTheFollowedVersion()
    {
        TransferReport report = await ReportAsync(
            Redirect(302),
            TimedOut(new TransferReport { ResponseCode = 530, ContentType = "text/x-own" }));

        AssertResponse(report, 530, System.Net.HttpVersion.Version11, "text/x-own");
    }

    [TestMethod]
    public async Task FollowAsync_FailedHopReceivedAResponse_ReportsItsOwn()
    {
        TransferReport report = await ReportAsync(
            Redirect(302),
            TimedOut(new TransferReport { ResponseCode = 200, HttpVersion = System.Net.HttpVersion.Version10 }));

        AssertResponse(report, 200, System.Net.HttpVersion.Version10, null);
    }

    [TestMethod]
    public async Task FollowAsync_FollowedHopSucceedsWithNoHttpResponse_ReportsNone()
    {
        TransferReport report = await ReportAsync(Redirect(302), TransferResult.Success(0) with { Report = new TransferReport() });

        AssertResponse(report, 0, null, null);
    }

    [TestMethod]
    public async Task FollowAsync_FirstHopHasNoResponse_ReportsNone()
    {
        TransferReport report = await ReportAsync(TimedOut(new TransferReport()));

        AssertResponse(report, 0, null, null);
    }

    private static void AssertResponse(TransferReport report, int code, Version? version, string? contentType)
    {
        Assert.AreEqual(code, report.ResponseCode);
        Assert.AreEqual(version, report.HttpVersion);
        Assert.AreEqual(contentType, report.ContentType);
    }

    private static TransferResult TimedOut(TransferReport report) =>
        TransferResult.Failure(CurlExitCode.OperationTimedOut, "Operation timed out after 2000 milliseconds with 0 bytes received", 0) with { Report = report };

    private static TransferResult Redirect(int code, Version? version = null, string contentType = "text/x-first") =>
        TransferResult.Success(0) with
        {
            Report = new TransferReport
            {
                ResponseCode = code,
                HttpVersion = version ?? System.Net.HttpVersion.Version11,
                ContentType = contentType,
                ResponseHeaders = [new("Location", "http://127.0.0.1:38193/")],
                RedirectUrl = "http://127.0.0.1:38193/",
            },
        };

    private async Task<TransferReport> ReportAsync(params TransferResult[] script)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1:38192/"),
            Output = Stream.Null,
            Http = new HttpRequestOptions { FollowRedirects = true },
        };
        diagnostics.Arrange("hops", script.Length);

        TransferResult result = await new RedirectFollower(new ProtocolDispatcher([new ScriptedHandler(script)])).FollowAsync(context, new RedirectPolicy());

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("response code", result.Report?.ResponseCode);
        return result.Report!;
    }

    private sealed class ScriptedHandler(TransferResult[] script) : IProtocolHandler
    {
        private int calls;

        public IReadOnlyCollection<string> SupportedSchemes => ["http"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context) => ValueTask.FromResult(script[calls++]);
    }
}
