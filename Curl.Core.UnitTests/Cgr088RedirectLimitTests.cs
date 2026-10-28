using Curl.Core.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Core;

/// <summary>
/// Pins that <see cref="RedirectFollower" /> echoes <c>Maximum (N) redirects followed</c> as an info
/// line when <c>-L</c> reaches <c>--max-redirs</c>, as curl 8.22.0's <c>-v</c> does (BL-3955).
/// </summary>
[TestClass]
public sealed class Cgr088RedirectLimitTests
{
    private readonly RecordingTransferEvents events = new();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task FollowAsync_MaxRedirsReached_ReportsTheLimitAsAnInfoLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("policy", "max redirects 0");

        TransferResult result;
        using (diagnostics.Phase("follow"))
        {
            result = await FollowAsync(new RedirectPolicy { MaxRedirects = 0 });
        }

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("infos", string.Join(" | ", events.Infos));
        diagnostics.Assert("exit code", CurlExitCode.TooManyRedirects, result.ExitCode);
        Assert.AreEqual(CurlExitCode.TooManyRedirects, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "Maximum (0) redirects followed" }, events.Infos);
    }

    private Task<TransferResult> FollowAsync(RedirectPolicy policy) =>
        new RedirectFollower(new ProtocolDispatcher([new RedirectingHandler()]))
            .FollowAsync(
                new TransferContext
                {
                    Url = CurlUrl.Parse("http://127.0.0.1:18907/"),
                    Output = Stream.Null,
                    Http = new HttpRequestOptions { FollowRedirects = true },
                    TimeProvider = TimeProvider.System,
                    Events = events,
                },
                policy)
            .AsTask();

    /// <summary>Serves http, answering every call with a 302.</summary>
    private sealed class RedirectingHandler : IProtocolHandler
    {
        public IReadOnlyCollection<string> SupportedSchemes => ["http"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context) =>
            ValueTask.FromResult(
                TransferResult.Success(0) with
                {
                    Report = new TransferReport { ResponseCode = 302, RedirectUrl = "http://127.0.0.1:18907/x" },
                });
    }
}
