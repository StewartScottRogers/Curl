using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Core;

/// <summary>
/// Pins CGR-062 (BL-3591, ADR-0829): curl 8.22.0 refuses a redirect to <c>http:</c>, with no slash
/// after the scheme, as <c>Unsupported number of slashes following scheme</c>, exit 3.
/// </summary>
[TestClass]
public sealed class Cgr062Tests
{
    [TestMethod]
    [DataRow("http:", DisplayName = "scheme only")]
    [DataRow("http:b", DisplayName = "no slash")]
    public async Task FollowAsync_TargetWithNoSlashAfterTheScheme_FailsWithTheSlashesReason(string target)
    {
        ScriptedHandler handler = new(TransferResult.Success(0) with { Report = new TransferReport { ResponseCode = 302, RedirectUrl = target } });

        TransferResult result = await new RedirectFollower(new ProtocolDispatcher([handler]))
            .FollowAsync(
                new TransferContext
                {
                    Url = CurlUrl.Parse("http://127.0.0.1:20500/a/b"),
                    Output = Stream.Null,
                    Http = new HttpRequestOptions { FollowRedirects = true },
                    TimeProvider = TimeProvider.System,
                },
                new RedirectPolicy());

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual("The redirect target URL could not be parsed: Unsupported number of slashes following scheme", result.ErrorMessage);
    }

    /// <summary>Serves http, answering each call with the next scripted result.</summary>
    private sealed class ScriptedHandler(params TransferResult[] script) : IProtocolHandler
    {
        private int calls;

        public IReadOnlyCollection<string> SupportedSchemes => ["http"];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context) => ValueTask.FromResult(script[calls++]);
    }
}
