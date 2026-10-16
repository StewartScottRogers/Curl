using System.Net;
using System.Text;
using Curl.Api.Cancellation;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Errors;
using Curl.Api.Info;
using Curl.Api.Messages;
using Curl.Api.Options;
using Curl.Api.RequestBodies;
using Curl.Api.ResponseBodies;
using CurlHttpVersion = Curl.Api.Options.Http.HttpVersion;

namespace Curl.Api.HttpMessageHandler.Core;

[TestClass]
public sealed class CurlHttpMessageHandlerTests
{
    private static async Task<T> WithHttpClientAsync<T>(IEasyTransfer easy, Func<HttpClient, Task<T>> use, CurlClientOptions? options = null)
    {
        await using CurlClient client = new(new ScriptedCurlEngine(easy), options ?? new CurlClientOptions(), ownsEngine: true);
        using HttpClient http = new(new CurlHttpMessageHandler(client));
        return await use(http);
    }

    private static ScriptedEasyTransfer Scripted(string body, params string[] headerLines)
    {
        ScriptedEasyTransfer easy = new() { BodyBytes = Encoding.UTF8.GetBytes(body) };
        easy.HeaderLines.AddRange(headerLines);
        return easy;
    }

    private static async Task<UploadingEasyTransfer> PostAsync(HttpContent content)
    {
        UploadingEasyTransfer easy = new();
        await WithHttpClientAsync(easy, http => http.PostAsync("http://example/form", content));
        return easy;
    }

    private static string Uploaded(UploadingEasyTransfer easy) => Encoding.UTF8.GetString(easy.Uploads.Single());

    [TestMethod]
    public void Constructor_NullClient_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlHttpMessageHandler(null!));
    }

    [TestMethod]
    public async Task Client_IsTheClientGiven()
    {
        await using CurlClient client = new(new ScriptedCurlEngine(new ScriptedEasyTransfer()));
        using CurlHttpMessageHandler handler = new(client);

        Assert.AreSame(client, handler.Client);
    }

    [TestMethod]
    public async Task Get_SendsMethodUrlVersionAndBothHeaderSets_AndMapsTheResponse()
    {
        ScriptedEasyTransfer easy = Scripted("hello", "HTTP/1.1 201 Made It\r\n", "Content-Type: text/plain\r\n", "X-Server: a\r\n", "\r\n");
        HttpRequestMessage request = new(HttpMethod.Get, "http://example/a?b=c");
        request.Headers.Accept.ParseAdd("a/b");
        request.Headers.Accept.ParseAdd("c/d");

        (HttpResponseMessage response, string body) = await WithHttpClientAsync(easy, async http =>
        {
            http.DefaultRequestHeaders.Add("X-One", "1");
            HttpResponseMessage sent = await http.SendAsync(request);
            return (sent, await sent.Content.ReadAsStringAsync());
        });

        EasyOptions options = easy.PerformedOptions!;
        Assert.AreEqual("http://example/a?b=c", options.Network.Url);
        Assert.IsNull(options.Transfer.CustomRequest);
        Assert.IsFalse(options.Http.Post);
        Assert.AreEqual(CurlHttpVersion.Http11, options.Http.HttpVersion);
        CollectionAssert.AreEquivalent(new[] { "Accept: a/b, c/d", "X-One: 1" }, options.Http.HttpHeaders.ToArray());
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.AreEqual("Made It", response.ReasonPhrase);
        Assert.AreEqual(HttpVersion.Version11, response.Version);
        Assert.AreEqual("a", response.Headers.GetValues("X-Server").Single());
        Assert.AreEqual("text/plain", response.Content.Headers.ContentType!.MediaType);
        Assert.IsFalse(response.Headers.NonValidated.Contains("Content-Type"));
        Assert.AreSame(request, response.RequestMessage);
        Assert.AreEqual("hello", body);
    }

    [TestMethod]
    public async Task Get_ResponseWithoutReason_GivesTheStatusPhrase()
    {
        ScriptedEasyTransfer easy = Scripted("", "HTTP/2 404\r\n", "\r\n");

        HttpResponseMessage response = await WithHttpClientAsync(easy, http => http.GetAsync("https://example/"));

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual("Not Found", response.ReasonPhrase);
        Assert.AreEqual(HttpVersion.Version20, response.Version);
    }

    [TestMethod]
    public async Task Send_KeepsTheClientsHttpOptions_WithTheRequestsVersion()
    {
        ScriptedEasyTransfer easy = Scripted("");
        CurlClientOptions options = new() { Http = new CurlHttpOptions { Referer = "http://from/" } };
        HttpRequestMessage request = new(HttpMethod.Get, "http://example/") { Version = HttpVersion.Version20 };

        await WithHttpClientAsync(easy, http => http.SendAsync(request), options);

        Assert.AreEqual("http://from/", easy.PerformedOptions!.Http.Referer);
        Assert.AreEqual(CurlHttpVersion.Http2, easy.PerformedOptions.Http.HttpVersion);
    }

    [TestMethod]
    public async Task Head_SetsNoBody_AndGivesAnEmptyContent()
    {
        ScriptedEasyTransfer easy = Scripted("", "HTTP/1.1 200 OK\r\n", "Content-Length: 42\r\n", "\r\n");

        (HttpResponseMessage response, byte[] body) = await WithHttpClientAsync(easy, async http =>
        {
            HttpResponseMessage sent = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, "http://example/"));
            return (sent, await sent.Content.ReadAsByteArrayAsync());
        });

        Assert.IsTrue(easy.PerformedOptions!.Transfer.NoBody);
        Assert.AreEqual(42, response.Content.Headers.ContentLength);
        Assert.IsEmpty(body);
    }

    [TestMethod]
    public async Task Post_StringContent_UploadsItWithItsTypeAndLength()
    {
        UploadingEasyTransfer easy = await PostAsync(new StringContent("hello", Encoding.UTF8, "text/plain"));

        Assert.AreEqual("hello", Uploaded(easy));
        EasyOptions options = easy.PerformedOptions!;
        Assert.IsTrue(options.Http.Post);
        Assert.AreEqual(5, options.Http.PostFieldSize);
        Assert.IsNull(options.Transfer.CustomRequest);
        CollectionAssert.AreEqual(new[] { "Content-Type: text/plain; charset=utf-8" }, options.Http.HttpHeaders.ToArray());
    }

    [TestMethod]
    public async Task Post_NonSeekableStreamContent_UploadsItChunked()
    {
        ScriptedStream stream = new(Encoding.UTF8.GetBytes("streamed")) { Seekable = false };

        UploadingEasyTransfer easy = await PostAsync(new System.Net.Http.StreamContent(stream));

        Assert.AreEqual("streamed", Uploaded(easy));
        Assert.AreEqual(-1, easy.PerformedOptions!.Http.PostFieldSize);
    }

    [TestMethod]
    public async Task Post_FormUrlEncodedContent_UploadsTheForm()
    {
        UploadingEasyTransfer easy = await PostAsync(new FormUrlEncodedContent([new("a", "1"), new("b", "x y")]));

        Assert.AreEqual("a=1&b=x+y", Uploaded(easy));
        CollectionAssert.Contains(easy.PerformedOptions!.Http.HttpHeaders.ToArray(), "Content-Type: application/x-www-form-urlencoded");
    }

    [TestMethod]
    public async Task Post_MultipartFormDataContent_UploadsThePartsUnderItsBoundary()
    {
        MultipartFormDataContent form = new("edge") { { new StringContent("v"), "field" } };

        UploadingEasyTransfer easy = await PostAsync(form);

        string uploaded = Uploaded(easy);
        StringAssert.StartsWith(uploaded, "--edge\r\n");
        StringAssert.Contains(uploaded, "name=field");
        StringAssert.EndsWith(uploaded, "\r\nv\r\n--edge--\r\n");
        Assert.AreEqual(uploaded.Length, easy.PerformedOptions!.Http.PostFieldSize);
        CollectionAssert.Contains(easy.PerformedOptions.Http.HttpHeaders.ToArray(), "Content-Type: multipart/form-data; boundary=\"edge\"");
    }

    [TestMethod]
    public async Task Put_SendsTheBodyUnderItsMethod()
    {
        UploadingEasyTransfer easy = new();

        await WithHttpClientAsync(easy, http => http.PutAsync("http://example/", new ByteArrayContent([1, 2])));

        Assert.AreEqual("PUT", easy.PerformedOptions!.Transfer.CustomRequest);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, easy.Uploads.Single());
    }

    [TestMethod]
    public async Task Body_StreamsInPieces_AsTheTransferDeliversThem()
    {
        SteppedEasyTransfer easy = new() { Info = new TransferInfo { ResponseCode = 200 } };
        easy.Deliver(1, 2);
        await using CurlClient client = new(new ScriptedCurlEngine(easy));
        using HttpClient http = new(new CurlHttpMessageHandler(client));

        using HttpResponseMessage response = await http.GetAsync("http://example/", HttpCompletionOption.ResponseHeadersRead);
        Stream body = await response.Content.ReadAsStreamAsync();
        byte[] buffer = new byte[16];

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(2, await body.ReadAsync(buffer));
        easy.Deliver(3);
        Assert.AreEqual(1, await body.ReadAsync(buffer));
        Assert.AreEqual(3, buffer[0]);
        easy.Finish(CurlCode.Ok);
        Assert.AreEqual(0, await body.ReadAsync(buffer));
    }

    [TestMethod]
    public async Task Trailers_FillTheTrailingHeaders_OnceTheBodyHasEnded()
    {
        ScriptedEasyTransfer easy = Scripted("abc", "HTTP/1.1 200 OK\r\n", "Transfer-Encoding: chunked\r\n", "\r\n", "X-Checksum: 99\r\n", "\r\n");

        (int before, HttpResponseMessage response) = await WithHttpClientAsync(easy, async http =>
        {
            HttpResponseMessage sent = await http.GetAsync("http://example/", HttpCompletionOption.ResponseHeadersRead);
            int trailersBefore = sent.TrailingHeaders.Count();
            _ = await sent.Content.ReadAsStringAsync();
            return (trailersBefore, sent);
        });

        Assert.AreEqual(0, before);
        Assert.AreEqual("99", response.TrailingHeaders.GetValues("X-Checksum").Single());
        Assert.IsTrue(response.Headers.TransferEncodingChunked);
    }

    [TestMethod]
    public async Task FailedTransfer_ThrowsHttpRequestException_WithTheCurlExceptionInside()
    {
        ScriptedEasyTransfer easy = new() { Result = CurlCode.CouldntConnect, ErrorText = "Failed to connect" };

        HttpRequestException exception = await Assert.ThrowsExactlyAsync<HttpRequestException>(
            () => WithHttpClientAsync(easy, http => http.GetAsync("http://example/")));

        CurlException inner = Assert.IsInstanceOfType<CurlException>(exception.InnerException);
        Assert.AreEqual(CurlCode.CouldntConnect, inner.Code);
        Assert.AreEqual("Failed to connect", exception.Message);
    }

    [TestMethod]
    public async Task HttpClientTimeout_AbortsTheTransfer_AsATaskCanceledExceptionOfATimeout()
    {
        CancellableCurlEngine engine = new();
        await using CurlClient client = new(engine);
        using HttpClient http = new(new CurlHttpMessageHandler(client)) { Timeout = TimeSpan.FromMilliseconds(50) };

        TaskCanceledException exception = await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => http.GetAsync("http://example/"));

        Assert.IsInstanceOfType<TimeoutException>(exception.InnerException);
        Assert.IsTrue(engine.Transfer.Disposed);
    }

    [TestMethod]
    public async Task CancelledToken_AbortsTheRunningTransfer()
    {
        CancellableCurlEngine engine = new() { Transfer = { Stage = TransferStage.Uploading } };
        await using CurlClient client = new(engine);
        using HttpClient http = new(new CurlHttpMessageHandler(client));
        using CancellationTokenSource cancellation = new();

        Task<HttpResponseMessage> sending = http.PostAsync("http://example/", new StringContent("body"), cancellation.Token);
        await engine.Transfer.Reached;
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => sending);
        Assert.IsTrue(engine.Transfer.Disposed);
    }

    [TestMethod]
    public async Task RelativeUri_ThrowsInvalidOperationException()
    {
        await using CurlClient client = new(new ScriptedCurlEngine(new ScriptedEasyTransfer()));
        using HttpMessageInvoker invoker = new(new CurlHttpMessageHandler(client));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri("/a", UriKind.Relative)), CancellationToken.None));
    }

    [TestMethod]
    public async Task NullRequest_ThrowsArgumentNullException()
    {
        await using CurlClient client = new(new ScriptedCurlEngine(new ScriptedEasyTransfer()));
        using HttpMessageInvoker invoker = new(new CurlHttpMessageHandler(client));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => invoker.SendAsync(null!, CancellationToken.None));
    }
}
