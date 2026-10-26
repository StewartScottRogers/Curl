using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Core;

/// <summary>
/// Pins CGR-061 of the 2026-10-25 gap report: an absolute redirect target holding a control
/// byte (0x01-0x1F) or DEL is refused by curl 8.22.0's URL parser, so
/// <see cref="RedirectFollower" /> exits 3 with <c>The redirect target URL could not be parsed:
/// Malformed input to a URL function</c> and sends no second request, where it used to report
/// <c>Bad hostname</c> for a control byte and follow a DEL.
/// </summary>
[TestClass]
public sealed class Cgr061Tests
{
    private const string MalformedMessage = "The redirect target URL could not be parsed: Malformed input to a URL function";

    [TestMethod]
    [DataRow("http://127.0.0.1:18590/b\u0001c", DisplayName = "0x01")]
    [DataRow("http://127.0.0.1:18590/b\tc", DisplayName = "TAB")]
    [DataRow("http://127.0.0.1:18590/b\u001Fc", DisplayName = "0x1F")]
    [DataRow("http://127.0.0.1:18590/b\u007Fc", DisplayName = "DEL")]
    public async Task FollowAsync_AbsoluteTargetHoldingAControlByteOrDel_Exits3MalformedAndSendsNoSecondRequest(string target)
    {
        // curl 8.22.0 -sSL, Location: http://127.0.0.1:18590/b<0x7F>c -> exit 3, "curl: (3) The redirect
        // target URL could not be parsed: Malformed input to a URL function", one request (BL-3590 Notes).
        ScriptedHandler handler = new(Response(302, target), Response(200));

        TransferResult result = await FollowAsync(handler);

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual(MalformedMessage, result.ErrorMessage);
        Assert.AreEqual(1, handler.Calls);
    }

    [TestMethod]
    public async Task FollowAsync_TargetWithEscapedControlByte_IsFollowed()
    {
        // curl 8.22.0 -sSL, Location: /b<TAB>c -> GET /b%09c, exit 0 (BL-3590 Notes).
        ScriptedHandler handler = new(Response(302, "http://127.0.0.1:18590/b%09c"), Response(200));

        TransferResult result = await FollowAsync(handler);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(2, handler.Calls);
    }

    private static Task<TransferResult> FollowAsync(ScriptedHandler handler) =>
        new RedirectFollower(new ProtocolDispatcher([handler]))
            .FollowAsync(
                new TransferContext
                {
                    Url = CurlUrl.Parse("http://127.0.0.1:18590/a/b"),
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
