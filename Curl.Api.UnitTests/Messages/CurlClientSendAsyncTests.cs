using Curl.Api.Codes;
using Curl.Api.Errors;
using Curl.Api.Info;
using Curl.Api.Options;

namespace Curl.Api.Messages;

[TestClass]
public sealed class CurlClientSendAsyncTests
{
    private static async Task<(CurlResponse Response, ScriptedEasyTransfer Easy)> SendAsync(
        CurlRequest request, ScriptedEasyTransfer? easy = null, CancellationToken cancellationToken = default)
    {
        easy ??= new ScriptedEasyTransfer();
        await using CurlClient client = new(new ScriptedCurlEngine(easy), new CurlClientOptions { UserAgent = "agent/1" }, ownsEngine: true);
        CurlResponse response = await client.SendAsync(request, cancellationToken);
        return (response, easy);
    }

    private static EasyOptions Performed(ScriptedEasyTransfer easy) => easy.PerformedOptions!;

    [TestMethod]
    public async Task SendAsync_RemovedHeader_IsSentAsNameColonAfterTheValuedAndEmptyHeaders()
    {
        CurlRequest request = new(HttpMethod.Get, "http://example/a")
        {
            Headers = [new("X-One", "1"), new("X-Empty", "")],
            RemovedHeaders = ["Accept", "Host"],
        };

        (_, ScriptedEasyTransfer easy) = await SendAsync(request);

        CollectionAssert.AreEqual(new[] { "X-One: 1", "X-Empty;", "Accept:", "Host:" }, Performed(easy).Http.HttpHeaders.ToArray());
    }

    [TestMethod]
    public async Task SendAsync_PutsUrlAndHeadersInTheOptions_KeepingClientDefaults()
    {
        CurlRequest request = new(HttpMethod.Get, "http://example/a")
        {
            Headers = [new("X-One", "1"), new("x-one", "2"), new("Accept", "")],
        };

        (_, ScriptedEasyTransfer easy) = await SendAsync(request);

        EasyOptions options = Performed(easy);
        Assert.AreEqual("http://example/a", options.Network.Url);
        CollectionAssert.AreEqual(new[] { "X-One: 1", "x-one: 2", "Accept;" }, options.Http.HttpHeaders.ToArray());
        Assert.AreEqual("agent/1", options.Http.UserAgent);
    }

    [TestMethod]
    public async Task SendAsync_Get_SetsNoMethodOptions()
    {
        (_, ScriptedEasyTransfer easy) = await SendAsync(new(HttpMethod.Get, "http://example/"));

        EasyOptions options = Performed(easy);
        Assert.IsNull(options.Transfer.CustomRequest);
        Assert.IsFalse(options.Transfer.NoBody);
        Assert.IsFalse(options.Http.Post);
        Assert.IsNull(options.Http.PostFields);
        Assert.AreEqual(-1, options.Http.PostFieldSize);
    }

    [TestMethod]
    public async Task SendAsync_Head_SetsNoBody()
    {
        (_, ScriptedEasyTransfer easy) = await SendAsync(new(HttpMethod.Head, "http://example/"));

        Assert.IsTrue(Performed(easy).Transfer.NoBody);
        Assert.IsNull(Performed(easy).Transfer.CustomRequest);
    }

    [TestMethod]
    public async Task SendAsync_PostWithoutBody_PostsAnEmptyBody()
    {
        (_, ScriptedEasyTransfer easy) = await SendAsync(new(HttpMethod.Post, "http://example/"));

        EasyOptions options = Performed(easy);
        Assert.IsTrue(options.Http.Post);
        Assert.AreEqual(0, options.Http.PostFields!.Value.Length);
        Assert.AreEqual(0, options.Http.PostFieldSize);
        Assert.IsNull(options.Transfer.CustomRequest);
    }

    [TestMethod]
    public async Task SendAsync_PutWithBody_PostsTheBodyUnderACustomMethod()
    {
        CurlRequest request = new(HttpMethod.Put, "http://example/") { Body = new byte[] { 1, 2, 3 } };

        (_, ScriptedEasyTransfer easy) = await SendAsync(request);

        EasyOptions options = Performed(easy);
        Assert.IsTrue(options.Http.Post);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, options.Http.PostFields!.Value.ToArray());
        Assert.AreEqual(3, options.Http.PostFieldSize);
        Assert.AreEqual("PUT", options.Transfer.CustomRequest);
    }

    [TestMethod]
    public async Task SendAsync_GetWithBody_KeepsGetAsACustomMethod()
    {
        CurlRequest request = new(HttpMethod.Get, "http://example/") { Body = new byte[] { 1 } };

        (_, ScriptedEasyTransfer easy) = await SendAsync(request);

        Assert.AreEqual("GET", Performed(easy).Transfer.CustomRequest);
    }

    [TestMethod]
    public async Task SendAsync_DeleteWithoutBody_IsACustomMethodWithoutPost()
    {
        (_, ScriptedEasyTransfer easy) = await SendAsync(new(HttpMethod.Delete, "http://example/"));

        Assert.AreEqual("DELETE", Performed(easy).Transfer.CustomRequest);
        Assert.IsFalse(Performed(easy).Http.Post);
    }

    [TestMethod]
    public async Task SendAsync_ResponseComesFromTransferInfoAndTheHeaderCallback()
    {
        TransferInfo info = new() { ResponseCode = 201, HttpVersion = 2, EffectiveUrl = "http://example/" };
        ScriptedEasyTransfer easy = new() { Info = info, BodyBytes = [1, 2] };
        easy.HeaderLines.AddRange(["HTTP/1.1 201 Created\r\n", "Content-Type: text/plain\r\n", "\r\n", "X-Sum: 9\r\n", "\r\n"]);

        (CurlResponse response, _) = await SendAsync(new(HttpMethod.Get, "http://example/"), easy);

        Assert.AreEqual(201, response.StatusCode);
        Assert.AreEqual("Created", response.ReasonPhrase);
        Assert.AreEqual(System.Net.HttpVersion.Version11, response.Version);
        CollectionAssert.AreEqual(
            new KeyValuePair<string, string>[] { new("Content-Type", "text/plain") }, response.Headers.ToArray());
        CollectionAssert.AreEqual(new KeyValuePair<string, string>[] { new("X-Sum", "9") }, response.Trailers.ToArray());
        Assert.AreSame(info, response.Info);
        Assert.AreEqual(2, easy.BodyBytesTaken);
        Assert.IsTrue(easy.Disposed);
        await using Stream body = response.Body;
        using MemoryStream copy = new();
        await body.CopyToAsync(copy);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, copy.ToArray());
    }

    [TestMethod]
    public async Task SendAsync_StatusLineDisagreesWithTransferInfo_TakesTheStatusLine()
    {
        ScriptedEasyTransfer easy = new() { Info = new() { ResponseCode = 0, HttpVersion = 0 } };
        easy.HeaderLines.AddRange(["HTTP/2 404\r\n", "\r\n"]);

        (CurlResponse response, _) = await SendAsync(new(HttpMethod.Get, "http://example/"), easy);

        Assert.AreEqual(404, response.StatusCode);
        Assert.AreEqual(System.Net.HttpVersion.Version20, response.Version);
    }

    [TestMethod]
    public async Task SendAsync_NoStatusLine_TakesStatusAndVersionFromTransferInfo()
    {
        ScriptedEasyTransfer easy = new() { Info = new() { ResponseCode = 226, HttpVersion = 0 } };

        (CurlResponse response, _) = await SendAsync(new(HttpMethod.Get, "ftp://example/f"), easy);

        Assert.AreEqual(226, response.StatusCode);
        Assert.AreEqual(System.Net.HttpVersion.Unknown, response.Version);
    }

    [TestMethod]
    public async Task SendAsync_PassesTheCancellationToken()
    {
        using CancellationTokenSource source = new();

        (CurlResponse response, ScriptedEasyTransfer easy) = await SendAsync(new(HttpMethod.Get, "http://example/"), cancellationToken: source.Token);
        await source.CancelAsync();

        Assert.IsTrue(easy.PerformedToken.IsCancellationRequested);
        await response.Body.DisposeAsync();
    }

    [TestMethod]
    public async Task SendAsync_ReturnsOnceTheBodyStarts_AndTheBodyStreamsTheRest()
    {
        ResponseBodies.SteppedEasyTransfer easy = new() { Info = new() { ResponseCode = 0 } };
        await using CurlClient client = new(new ScriptedCurlEngine(easy));
        Task<CurlResponse> sending = client.SendWithBodyCapacityAsync(new(HttpMethod.Get, "http://example/"), 2, CancellationToken.None);
        easy.Options.Callbacks.Header!("HTTP/1.1 200 OK\r\n"u8);
        easy.Options.Callbacks.Header!("\r\n"u8);
        easy.Deliver(1, 2);

        CurlResponse response = await sending;
        easy.Deliver(3, 4, 5);
        easy.Finish(CurlCode.Ok);
        await using Stream body = response.Body;
        using MemoryStream copy = new();
        await body.CopyToAsync(copy);

        Assert.AreEqual(200, response.StatusCode);
        Assert.AreEqual(System.Net.HttpVersion.Version11, response.Version);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5 }, copy.ToArray());
    }

    [TestMethod]
    public async Task SendAsync_FailedTransfer_ThrowsCurlExceptionAndDisposesTheHandle()
    {
        ScriptedEasyTransfer easy = new() { Result = CurlCode.CouldntConnect, ErrorText = "Failed to connect" };

        CurlException exception = await Assert.ThrowsExactlyAsync<CurlException>(
            () => SendAsync(new(HttpMethod.Get, "http://example/"), easy));

        Assert.AreEqual(CurlCode.CouldntConnect, exception.Code);
        Assert.AreEqual("Failed to connect", exception.Message);
        Assert.IsTrue(easy.Disposed);
    }

    [TestMethod]
    public async Task SendAsync_NullRequest_Throws()
    {
        await using CurlClient client = new(new ScriptedCurlEngine(new ScriptedEasyTransfer()));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => client.SendAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task SendAsync_AfterDispose_Throws()
    {
        CurlClient client = new(new ScriptedCurlEngine(new ScriptedEasyTransfer()));
        await client.DisposeAsync();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
            () => client.SendAsync(new(HttpMethod.Get, "http://example/"), CancellationToken.None));
    }

    [TestMethod]
    [DataRow(0L, "0.0")]
    [DataRow(1L, "1.0")]
    [DataRow(2L, "1.1")]
    [DataRow(3L, "2.0")]
    [DataRow(30L, "3.0")]
    [DataRow(4L, "0.0")]
    public void VersionOf_MapsCurlHttpVersionValues(long httpVersion, string expected) =>
        Assert.AreEqual(Version.Parse(expected), CurlClient.VersionOf(httpVersion));
}
