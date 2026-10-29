using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Core;

/// <summary>
/// Pins CGR-076 (BL-3605): a redirect to another host drops an <c>-H</c> <c>Authorization</c>
/// value and marks the hop <see cref="HttpRequestOptions.AuthorizationHeaderWithheld" />, so the
/// HTTP handler generates no Basic value from the Location's user information either, as curl
/// 8.22.0 sends no <c>Authorization</c> to <c>http://x:y@localhost:18605/ok</c> (BL-3605 Notes).
/// </summary>
[TestClass]
public sealed class Cgr076RedirectFollowerTests
{
    private const string Start = "http://127.0.0.1:18605/a";

    [TestMethod]
    [DataRow("Authorization: Bearer xyz", true, DisplayName = "Authorization withheld")]
    [DataRow("authorization : Bearer xyz", true, DisplayName = "Any case, blank before colon")]
    [DataRow("Cookie: c=1", false, DisplayName = "Cookie alone")]
    public async Task FollowAsync_ToAnotherHost_MarksAWithheldAuthorizationHeader(string header, bool withheld)
    {
        ScriptedHandler handler = new(Response(302, "http://x:y@localhost:18605/ok"), Response(200));

        await FollowAsync(handler, [header, "X-Custom: 1"]);

        Assert.AreEqual(withheld, handler.LastHttp!.AuthorizationHeaderWithheld);
        CollectionAssert.AreEqual(new[] { "X-Custom: 1" }, handler.LastHttp.Headers.ToArray());
    }

    [TestMethod]
    public async Task FollowAsync_ToTheSameOrigin_KeepsTheAuthorizationHeader()
    {
        ScriptedHandler handler = new(Response(302, "http://x:y@127.0.0.1:18605/ok"), Response(200));

        await FollowAsync(handler, ["Authorization: Bearer xyz"]);

        Assert.IsFalse(handler.LastHttp!.AuthorizationHeaderWithheld);
        CollectionAssert.AreEqual(new[] { "Authorization: Bearer xyz" }, handler.LastHttp.Headers.ToArray());
    }

    private static Task<TransferResult> FollowAsync(ScriptedHandler handler, string[] headers) =>
        new RedirectFollower(new ProtocolDispatcher([handler]))
            .FollowAsync(
                new TransferContext
                {
                    Url = CurlUrl.Parse(Start),
                    Output = Stream.Null,
                    Http = new HttpRequestOptions { FollowRedirects = true, Headers = headers },
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

        public HttpRequestOptions? LastHttp { get; private set; }

        public IReadOnlyCollection<string> SupportedSchemes => ["http"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            LastHttp = context.Http;
            return ValueTask.FromResult(script[calls++]);
        }
    }
}
