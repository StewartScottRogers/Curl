using System.Text;

namespace Curl.Extended.Protocol.AzureStorage.Http;

/// <summary>Pins the Azure Storage HTTP/1.1 exchange against RFC 9112 over a connection replaying recorded bytes.</summary>
[TestClass]
public sealed class AzureStorageHttpClientConnectionTests
{
    private static readonly KeyValuePair<string, string>[] HostOnly = [new("Host", "account.blob.core.windows.net")];

    [TestMethod]
    public async Task SendAsync_GetWithoutBody_WritesRequestLineFieldsAndEmptyLine()
    {
        // RFC 9112 §3: method SP request-target SP HTTP-version CRLF; §5: field-name ":" OWS field-value; then CRLF.
        var connection = new RecordedConnection("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");

        await new AzureStorageHttpClientConnection(connection).SendAsync(Get(), CancellationToken.None);

        Assert.AreEqual("GET /container/blob HTTP/1.1\r\nHost: account.blob.core.windows.net\r\n\r\n", connection.Written);
        Assert.AreEqual(1, connection.FlushCount);
    }

    [TestMethod]
    public async Task SendAsync_PutWithBody_AddsContentLengthAndStreamsTheBody()
    {
        // RFC 9112 §6.2: a request body's length is announced by Content-Length.
        var connection = new RecordedConnection("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");
        var request = new AzureStorageHttpRequest("PUT", "/container/blob", HostOnly, new MemoryStream(Encoding.ASCII.GetBytes("hello world")), 5);

        await new AzureStorageHttpClientConnection(connection).SendAsync(request, CancellationToken.None);

        Assert.AreEqual("PUT /container/blob HTTP/1.1\r\nHost: account.blob.core.windows.net\r\nContent-Length: 5\r\n\r\nhello", connection.Written);
    }

    [TestMethod]
    public async Task SendAsync_PutWithEmptyBody_SendsContentLengthZero()
    {
        var connection = new RecordedConnection("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");

        await new AzureStorageHttpClientConnection(connection).SendAsync(new AzureStorageHttpRequest("PUT", "/b/k", HostOnly, new MemoryStream(), 0), CancellationToken.None);

        StringAssert.EndsWith(connection.Written, "Content-Length: 0\r\n\r\n");
    }

    [TestMethod]
    public async Task SendAsync_BodyShorterThanItsLength_FailsRequestBodyTooShort()
    {
        var connection = new RecordedConnection(string.Empty);
        var request = new AzureStorageHttpRequest("PUT", "/b/k", HostOnly, new MemoryStream([1, 2]), 3);

        await AssertFailureAsync(AzureStorageHttpFailure.RequestBodyTooShort, () => new AzureStorageHttpClientConnection(connection).SendAsync(request, CancellationToken.None));
    }

    [TestMethod]
    [DataRow("X-Bad\r\nInjected", "v")]
    [DataRow("X-Bad", "v\nInjected: 1")]
    public async Task SendAsync_FieldHoldsCrOrLf_ThrowsArgumentException(string name, string value)
    {
        var request = new AzureStorageHttpRequest("GET", "/", [new(name, value)]);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => new AzureStorageHttpClientConnection(new RecordedConnection(string.Empty)).SendAsync(request, CancellationToken.None));
    }

    [TestMethod]
    public async Task SendAsync_NullRequest_ThrowsArgumentNullException() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new AzureStorageHttpClientConnection(new RecordedConnection(string.Empty)).SendAsync(null!, CancellationToken.None));

    [TestMethod]
    public async Task SendAsync_ContentLengthResponse_ReadsStatusFieldsAndExactlyThatManyBytes()
    {
        // RFC 9112 §4 status line, §5 fields with OWS trimmed, §6.3 rule 6 Content-Length; bytes arrive one at a time.
        var response = await SendAsync("HTTP/1.1 200 OK\r\nETag:  \"abc\"\t\r\nContent-Length: 5\r\n\r\nhelloEXTRA", readSize: 1);

        Assert.AreEqual(200, response.StatusCode);
        Assert.AreEqual("OK", response.ReasonPhrase);
        Assert.AreEqual("\"abc\"", response.FindHeader("etag"));
        Assert.IsNull(response.FindHeader("Missing"));
        Assert.AreEqual("hello", await ReadToEndAsync(response.Body));
        Assert.IsTrue(response.ConnectionPersists);
    }

    [TestMethod]
    public async Task SendAsync_ContentLengthBodyCutShort_FailsClosedEarly()
    {
        // RFC 9112 §8: a message whose Content-Length is not received in full is incomplete.
        var response = await SendAsync("HTTP/1.1 200 OK\r\nContent-Length: 10\r\n\r\nhello");

        await AssertFailureAsync(AzureStorageHttpFailure.ClosedEarly, () => ReadToEndAsync(response.Body));
    }

    [TestMethod]
    [DataRow("abc")]
    [DataRow("-1")]
    [DataRow("1, 1")]
    public async Task SendAsync_InvalidContentLength_FailsInvalidContentLength(string contentLength) =>
        await AssertFailureAsync(AzureStorageHttpFailure.InvalidContentLength, () => SendAsync($"HTTP/1.1 200 OK\r\nContent-Length: {contentLength}\r\n\r\n"));

    [TestMethod]
    public async Task SendAsync_ChunkedResponse_JoinsChunksIgnoresExtensionsAndDiscardsTrailers()
    {
        // RFC 9112 §7.1: chunk-size [ chunk-ext ] CRLF chunk-data CRLF, last-chunk, trailer section, CRLF.
        var response = await SendAsync(
            "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n5;name=value\r\nhello\r\nA \r\n, world!!!\r\n0\r\nX-Trailer: 1\r\n\r\n",
            readSize: 3);

        Assert.AreEqual("hello, world!!!", await ReadToEndAsync(response.Body));
        Assert.AreEqual(0, await response.Body.ReadAsync(new byte[4]));
        Assert.IsTrue(response.ConnectionPersists);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\nzz\r\n", DisplayName = "§7.1 chunk-size is HEXDIG")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n\r\n", DisplayName = "§7.1 chunk-size is 1*HEXDIG")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\nFFFFFFFFFFFFFFFF\r\n", DisplayName = "§7.1 a size past long is not a size")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n2\r\nabX\r\n", DisplayName = "§7.1 chunk-data is followed by CRLF")]
    public async Task SendAsync_MalformedChunk_FailsInvalidChunk(string recorded)
    {
        var response = await SendAsync(recorded);

        await AssertFailureAsync(AzureStorageHttpFailure.InvalidChunk, () => ReadToEndAsync(response.Body));
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n", DisplayName = "before a chunk-size line")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhel", DisplayName = "inside chunk-data")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n0\r\nX-Trailer: 1\r\n", DisplayName = "inside the trailer section")]
    public async Task SendAsync_ChunkedBodyCutShort_FailsClosedEarly(string recorded)
    {
        // RFC 9112 §8: a chunked message without its last-chunk and final CRLF is incomplete.
        var response = await SendAsync(recorded);

        await AssertFailureAsync(AzureStorageHttpFailure.ClosedEarly, () => ReadToEndAsync(response.Body));
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\n\r\nall of it", DisplayName = "§6.3 rule 8: no framing")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip\r\n\r\nall of it", DisplayName = "§6.3 rule 4: chunked is not the final coding")]
    public async Task SendAsync_CloseDelimitedResponse_ReadsUntilCloseAndDoesNotPersist(string recorded)
    {
        var response = await SendAsync(recorded);

        Assert.AreEqual("all of it", await ReadToEndAsync(response.Body));
        Assert.IsFalse(response.ConnectionPersists);
    }

    [TestMethod]
    [DataRow("HEAD", "HTTP/1.1 200 OK\r\nContent-Length: 42\r\n\r\n", DisplayName = "answer to HEAD")]
    [DataRow("GET", "HTTP/1.1 204 No Content\r\n\r\n", DisplayName = "204")]
    [DataRow("GET", "HTTP/1.1 304 Not Modified\r\nContent-Length: 42\r\n\r\n", DisplayName = "304")]
    [DataRow("GET", "HTTP/1.1 100 Continue\r\n\r\n", DisplayName = "1xx")]
    public async Task SendAsync_ResponseWithoutBody_HasAnEmptyBodyAndPersists(string method, string recorded)
    {
        // RFC 9112 §6.3 rule 1: these responses end at the empty line after the fields.
        var response = await new AzureStorageHttpClientConnection(new RecordedConnection(recorded + "NEXT")).SendAsync(new AzureStorageHttpRequest(method, "/", HostOnly), CancellationToken.None);

        Assert.AreEqual(string.Empty, await ReadToEndAsync(response.Body));
        Assert.IsTrue(response.ConnectionPersists);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nConnection: keep-alive, close\r\nContent-Length: 0\r\n\r\n", DisplayName = "§9.6 Connection: close")]
    [DataRow("HTTP/1.0 200 OK\r\nContent-Length: 0\r\n\r\n", DisplayName = "§9.3 HTTP/1.0")]
    public async Task SendAsync_ResponseEndingTheConnection_DoesNotPersist(string recorded) =>
        Assert.IsFalse((await SendAsync(recorded)).ConnectionPersists);

    [TestMethod]
    public async Task SendAsync_ConnectionKeepAlive_Persists() =>
        Assert.IsTrue((await SendAsync("HTTP/1.1 200 OK\r\nConnection: keep-alive\r\nContent-Length: 0\r\n\r\n")).ConnectionPersists);

    [TestMethod]
    public async Task SendAsync_TwoExchangesOnOneConnection_ReadsEachResponseInTurn()
    {
        // RFC 9112 §9.3: a persistent connection carries the next request after the previous response ends.
        var client = new AzureStorageHttpClientConnection(new RecordedConnection(
            "HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\noneHTTP/1.1 201 Created\r\nContent-Length: 3\r\n\r\ntwo"));

        var first = await client.SendAsync(Get(), CancellationToken.None);
        var firstBody = await ReadToEndAsync(first.Body);
        var second = await client.SendAsync(Get(), CancellationToken.None);

        Assert.AreEqual("one", firstBody);
        Assert.AreEqual(201, second.StatusCode);
        Assert.AreEqual("two", await ReadToEndAsync(second.Body));
    }

    [TestMethod]
    public async Task SendAsync_StatusLineWithoutReason_HasAnEmptyReasonPhrase()
    {
        // RFC 9112 §4: status-line = HTTP-version SP status-code SP [ reason-phrase ]; a bare LF ends a line too (§2.2).
        var response = await SendAsync("HTTP/1.1 404\nContent-Length: 0\n\n");

        Assert.AreEqual(404, response.StatusCode);
        Assert.AreEqual(string.Empty, response.ReasonPhrase);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 20", DisplayName = "too short")]
    [DataRow("HTTX/1.1 200 OK", DisplayName = "not HTTP")]
    [DataRow("HTTP/1.x 200 OK", DisplayName = "minor version not a digit")]
    [DataRow("HTTP/1.11200 OK", DisplayName = "no SP after the version")]
    [DataRow("HTTP/1.1 2x0 OK", DisplayName = "status code not 3DIGIT")]
    [DataRow("HTTP/1.1 2000 OK", DisplayName = "no SP after the status code")]
    public async Task SendAsync_MalformedStatusLine_FailsMalformedStatusLine(string statusLine) =>
        await AssertFailureAsync(AzureStorageHttpFailure.MalformedStatusLine, () => SendAsync(statusLine + "\r\n\r\n"));

    [TestMethod]
    [DataRow("No colon", DisplayName = "§5.1 no colon")]
    [DataRow(": value", DisplayName = "§5.1 empty field-name")]
    [DataRow("Name : value", DisplayName = "§5.1 whitespace before the colon")]
    [DataRow(" folded", DisplayName = "§5.2 obs-fold")]
    [DataRow("\tfolded", DisplayName = "§5.2 obs-fold with a tab")]
    public async Task SendAsync_MalformedField_FailsMalformedHeaderField(string field) =>
        await AssertFailureAsync(AzureStorageHttpFailure.MalformedHeaderField, () => SendAsync($"HTTP/1.1 200 OK\r\nA: 1\r\n{field}\r\n\r\n"));

    [TestMethod]
    [DataRow("", DisplayName = "before the status line")]
    [DataRow("HTTP/1.1 200 OK", DisplayName = "inside the status line")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n", DisplayName = "before the empty line")]
    public async Task SendAsync_ConnectionClosedInTheHead_FailsClosedEarly(string recorded) =>
        await AssertFailureAsync(AzureStorageHttpFailure.ClosedEarly, () => SendAsync(recorded));

    [TestMethod]
    public async Task SendAsync_LineLongerThanTheLimit_FailsLineTooLong() =>
        await AssertFailureAsync(AzureStorageHttpFailure.LineTooLong, () => SendAsync("HTTP/1.1 200 OK\r\nX-Long: " + new string('a', AzureStorageHttpConnectionReader.MaxLineLength) + "\r\n\r\n"));

    private static AzureStorageHttpRequest Get() => new("GET", "/container/blob", HostOnly);

    private static Task<AzureStorageHttpResponse> SendAsync(string recorded, int readSize = int.MaxValue) =>
        new AzureStorageHttpClientConnection(new RecordedConnection(recorded, readSize)).SendAsync(Get(), CancellationToken.None);

    private static async Task<string> ReadToEndAsync(Stream body)
    {
        using var copy = new MemoryStream();
        await body.CopyToAsync(copy);
        return Encoding.Latin1.GetString(copy.ToArray());
    }

    private static async Task AssertFailureAsync(AzureStorageHttpFailure failure, Func<Task> action) =>
        Assert.AreEqual(failure, (await Assert.ThrowsExactlyAsync<AzureStorageHttpException>(action)).Failure);
}
