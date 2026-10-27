using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Core;

/// <summary>
/// Pins CGR-087 (BL-3616): without <c>-L</c>, the redirect target a handler reports reaches
/// <c>%{redirect_url}</c> whole, whatever its scheme. Measured against curl 8.22.0 (Schannel)
/// with <c>Record-CurlExchange.ps1</c> on 2026-10-26: <c>curl -sS -w '%{redirect_url}\n'</c>
/// writes <c>foo://x/</c> for <c>Location: foo://x/</c> and
/// <c>http://127.0.0.1:20716/caf%C3%A9?q=%E9</c> for <c>Location: /caf\xc3\xa9?q=\xe9</c>.
/// </summary>
[TestClass]
public sealed class Cgr087Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("foo://x/")]
    [DataRow("http://127.0.0.1:20716/caf%C3%A9?q=%E9")]
    public async Task FollowAsync_WithoutLocationOption_KeepsRedirectUrlOfAnyScheme(string redirectUrl)
    {
        OneAnswerHandler handler = new(TransferResult.Success(0) with
        {
            Report = new TransferReport { ResponseCode = 302, RedirectUrl = redirectUrl },
        });
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1:20716/raw/protoloc"),
            Output = Stream.Null,
            Http = new HttpRequestOptions(),
            TimeProvider = TimeProvider.System,
        };
        Diagnostics.Arrange("RedirectUrl", redirectUrl);

        TransferResult result = await new RedirectFollower(new ProtocolDispatcher([handler])).FollowAsync(context, new RedirectPolicy());

        Diagnostics.Assert("result.ExitCode", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("result.Report.RedirectUrl", redirectUrl, result.Report!.RedirectUrl);
        Assert.AreEqual(redirectUrl, result.Report!.RedirectUrl);
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
