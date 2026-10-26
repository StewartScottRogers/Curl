using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Core;

/// <summary>
/// Pins CGR-086 (BL-3615): <c>-L</c> to a 301 whose <c>Location</c> cannot be parsed fails the
/// transfer as curl does, rather than taking the 301 as the final response. Measured against
/// curl 8.22.0 (Schannel) with <c>Record-CurlExchange.ps1</c> on 2026-10-26:
/// <c>curl -sS -L -w '%{exitcode}|%{errormsg}|%{num_redirects}\n'</c> writes
/// <c>3|The redirect target URL could not be parsed: Bad IPv6 address|1</c> for
/// <c>Location: http://[bad</c> and
/// <c>1|The redirect target URL could not be parsed: Unsupported URL scheme|1</c> for
/// <c>Location: foo://x/</c>.
/// </summary>
[TestClass]
public sealed class Cgr086Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("http://[bad", CurlExitCode.UrlMalformat, "Bad IPv6 address")]
    [DataRow("foo://x/", CurlExitCode.UnsupportedProtocol, "Unsupported URL scheme")]
    public async Task FollowAsync_MovedPermanentlyToLocationThatCannotBeParsed_FailsAfterCountingOneRedirect(
        string location,
        CurlExitCode exitCode,
        string reason)
    {
        OneAnswerHandler handler = new(TransferResult.Success(0) with
        {
            Report = new TransferReport { ResponseCode = 301, RedirectUrl = location },
        });
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1:20700/raw/badloc"),
            Output = Stream.Null,
            Http = new HttpRequestOptions { FollowRedirects = true },
            TimeProvider = TimeProvider.System,
        };
        Diagnostics.Arrange("Location", location);

        TransferResult result = await new RedirectFollower(new ProtocolDispatcher([handler])).FollowAsync(context, new RedirectPolicy());

        Diagnostics.Assert("result.ExitCode", exitCode, result.ExitCode);
        Assert.AreEqual(exitCode, result.ExitCode);
        Diagnostics.Assert("result.ErrorMessage", $"The redirect target URL could not be parsed: {reason}", result.ErrorMessage);
        Assert.AreEqual($"The redirect target URL could not be parsed: {reason}", result.ErrorMessage);
        Diagnostics.Assert("result.Report.RedirectCount", 1, result.Report!.RedirectCount);
        Assert.AreEqual(1, result.Report!.RedirectCount);
        Diagnostics.Assert("handler.Calls", 1, handler.Calls);
        Assert.AreEqual(1, handler.Calls);
    }

    private sealed class OneAnswerHandler(TransferResult answer) : IProtocolHandler
    {
        public int Calls { get; private set; }

        public IReadOnlyCollection<string> SupportedSchemes => ["http"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            Calls++;
            return ValueTask.FromResult(answer);
        }
    }
}
