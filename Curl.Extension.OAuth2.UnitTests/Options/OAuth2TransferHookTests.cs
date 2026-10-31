using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.OAuth2.Cache;
using Curl.Extension.OAuth2.Flows;
using Curl.Extension.OAuth2.Http;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OAuth2.Options;

/// <summary>
/// Pins that the hook gets a token before the transfer and sends it as a Bearer token (RFC 6750 §2.1), from the cache when
/// it has one, and never sends the transfer when it gets none (ADR-1075).
/// </summary>
[TestClass]
public sealed class OAuth2TransferHookTests
{
    private const string TokenUrl = "https://server.example.com/token";

    [TestMethod]
    public async Task ExecuteAsync_TokenIssued_SendsTheTransferWithTheBearerTokenAfterCurlsOwnHeaders()
    {
        var connection = new RecordedConnection(FlowResponses.Token());
        ITransferContext? sent = null;

        var result = await Hook(null).ExecuteAsync(Context(), Services(new QueuedConnector(connection)), context =>
        {
            sent = context;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "X-Own: 1", "Authorization: Bearer 2YotnFZFEjr1zCsicMWpAA" }, sent?.Http?.Headers.ToArray());
        StringAssert.Contains(connection.Written, "\r\n\r\ngrant_type=client_credentials");
    }

    [TestMethod]
    public async Task ExecuteAsync_TokenCached_SendsTheCachedTokenWithoutAskingAgain()
    {
        using var directory = new CacheTestDirectory();
        var first = new QueuedConnector(new RecordedConnection(FlowResponses.Token("cached")));
        var second = new QueuedConnector();
        List<string> headers = [];

        await Hook(directory.Path).ExecuteAsync(Context(), Services(first), Recording(headers));
        var result = await Hook(directory.Path).ExecuteAsync(Context(), Services(second), Recording(headers));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0, second.ConnectCount);
        CollectionAssert.AreEqual(new[] { "Authorization: Bearer cached", "Authorization: Bearer cached" }, headers);
    }

    [TestMethod]
    public async Task ExecuteAsync_TokenRefused_FailsWithTheFlowsExitCodeAndNeverSendsTheTransfer()
    {
        var connector = new QueuedConnector(new RecordedConnection(FlowResponses.Error("invalid_client", 401, "Unauthorized")));

        var result = await Hook(null).ExecuteAsync(Context(), Services(connector), NeverSent);

        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual("The token endpoint refused the request: invalid_client.", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_TokenNotBearer_FailsWithWeirdServerReply()
    {
        var connector = new QueuedConnector(new RecordedConnection(FlowResponses.Json("""{"access_token":"x","token_type":"mac"}""")));

        var result = await Hook(null).ExecuteAsync(Context(), Services(connector), NeverSent);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("The token endpoint gave a token of type \"mac\", which curl cannot send.", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_WrongOptions_FailsWithExit2AndNeverSendsTheTransfer()
    {
        var hook = new OAuth2TransferHook(null, "--oauth2-device needs --oauth2-client-id.", TextWriter.Null, count => new byte[count]);

        var result = await hook.ExecuteAsync(Context(), Services(new QueuedConnector()), NeverSent);

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        Assert.AreEqual("--oauth2-device needs --oauth2-client-id.", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoServices_FailsWithExit2()
    {
        ITransferHook hook = Hook(null);

        var result = await hook.ExecuteAsync(Context(), NeverSent);

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        Assert.AreEqual(OAuth2TransferHook.NoServicesMessage, result.ErrorMessage);
    }

    private static OAuth2TransferHook Hook(string? cacheDirectory) => new(
        new OAuth2Configuration(
            OAuth2FlowKind.ClientCredentials,
            new OAuth2FlowRequest(new Uri(TokenUrl), new Wire.OAuth2Client("s6BhdRkqt3", null, Wire.OAuth2ClientAuthentication.Basic), null),
            null,
            0,
            cacheDirectory),
        null,
        TextWriter.Null,
        count => new byte[count]);

    private static TransferHookServices Services(IConnector connector) =>
        new(connector, new RecordedListener(ListenResult.Failed(CurlExitCode.CouldntConnect, "no")));

    private static TransferContext Context() => new()
    {
        Url = CurlUrl.Parse("https://api.example.com/resource"),
        Output = new MemoryStream(),
        Http = new Protocol.Abstractions.HttpRequestOptions { Headers = ["X-Own: 1"] },
    };

    private static Func<ITransferContext, ValueTask<TransferResult>> Recording(List<string> headers) => context =>
    {
        headers.Add(context.Http!.Headers[^1]);
        return ValueTask.FromResult(TransferResult.Success(0));
    };

    private static ValueTask<TransferResult> NeverSent(ITransferContext context) => throw new AssertFailedException("The transfer was sent.");
}
