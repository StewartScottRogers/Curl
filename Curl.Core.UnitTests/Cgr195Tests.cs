using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Core;

/// <summary>
/// Pins CGR-195 (BL-3724): a redirect target curl 8.22.0 cannot parse is refused with the
/// reason curl gives for its shape, not always <c>Bad hostname</c>.
/// </summary>
[TestClass]
public sealed class Cgr195Tests
{
    private const string Prefix = "The redirect target URL could not be parsed: ";

    [TestMethod]
    [DataRow("http:x", "Unsupported number of slashes following scheme")]
    [DataRow("http:////", "Unsupported number of slashes following scheme")]
    [DataRow("http://", "No host present")]
    [DataRow("http://:80/", "No host present")]
    [DataRow("file://localhost:1/y", "Bad file:// URL")]
    [DataRow("http://h:99999/", "Port number was not a decimal number between 0 and 65535")]
    [DataRow("http://a b/", "Bad hostname")]
    public async Task FollowAsync_UnparsableTarget_GivesCurlsReasonForItsShape(string target, string reason)
    {
        // curl 8.22.0 Schannel -sS -L, Location: <target> -> exit 3, "curl: (3) <Prefix><reason>".
        TransferResult result = await FollowAsync(new ScriptedHandler(Response(302, target)));

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual(Prefix + reason, result.ErrorMessage);
    }

    private static Task<TransferResult> FollowAsync(ScriptedHandler handler) =>
        new RedirectFollower(new ProtocolDispatcher([handler]))
            .FollowAsync(
                new TransferContext
                {
                    Url = CurlUrl.Parse("http://127.0.0.1:18867/a/"),
                    Output = Stream.Null,
                    Http = new HttpRequestOptions { FollowRedirects = true },
                    TimeProvider = TimeProvider.System,
                },
                new RedirectPolicy())
            .AsTask();

    private static TransferResult Response(int status, string location) =>
        TransferResult.Success(0) with { Report = new TransferReport { ResponseCode = status, RedirectUrl = location } };

    private sealed class ScriptedHandler(params TransferResult[] script) : IProtocolHandler
    {
        private int calls;

        public IReadOnlyCollection<string> SupportedSchemes => ["http"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context) => ValueTask.FromResult(script[calls++]);
    }
}
