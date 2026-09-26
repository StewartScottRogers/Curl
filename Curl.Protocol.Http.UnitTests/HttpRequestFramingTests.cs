using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpRequestFraming" />: the method, chunking and <c>100 Continue</c> wait
/// curl 8.21.0 chose for the requests measured in the BL-175 Notes.
/// </summary>
[TestClass]
public sealed class HttpRequestFramingTests
{
    [TestMethod]
    public void Of_NoBody_IsGetWithNothingToFrame()
    {
        HttpRequestFraming framing = Of(new HttpRequestOptions());

        Assert.AreEqual("GET", framing.Method);
        Assert.IsNull(framing.Body);
        Assert.IsNull(framing.KnownLength);
        Assert.IsFalse(framing.IsChunked);
        Assert.IsFalse(framing.AddsExpect);
        Assert.IsFalse(framing.AwaitsContinue);
    }

    [TestMethod]
    public void Of_SmallBody_IsPostWithContentLengthAndNoWait()
    {
        BytesBody body = new("x=1"u8.ToArray(), "application/x-www-form-urlencoded");

        HttpRequestFraming framing = Of(new HttpRequestOptions { Body = body });

        Assert.AreEqual("POST", framing.Method);
        Assert.AreSame(body, framing.Body);
        Assert.AreEqual(3L, framing.KnownLength);
        Assert.IsFalse(framing.IsChunked);
        Assert.IsFalse(framing.AddsExpect);
        Assert.IsFalse(framing.AwaitsContinue);
    }

    [TestMethod]
    public void Of_CustomMethodWithoutBody_KeepsTheMethod() =>
        Assert.AreEqual("DELETE", Of(new HttpRequestOptions { CustomMethod = "DELETE" }).Method);

    [TestMethod]
    [DataRow(1048576L, false, DisplayName = "1 MiB")]
    [DataRow(1048577L, true, DisplayName = "1 MiB + 1")]
    public void Of_StreamOfKnownLength_ExpectsAboveOneMebibyte(long length, bool expects)
    {
        HttpRequestFraming framing = Of(new HttpRequestOptions { Body = new StreamBody(Stream.Null, length, "a/b") });

        Assert.AreEqual(length, framing.KnownLength);
        Assert.IsFalse(framing.IsChunked);
        Assert.AreEqual(expects, framing.AddsExpect);
        Assert.AreEqual(expects, framing.AwaitsContinue);
    }

    [TestMethod]
    public void Of_StreamOfUnknownLength_IsChunkedAndExpects()
    {
        HttpRequestFraming framing = Of(new HttpRequestOptions { Body = new StreamBody(Stream.Null, null, "a/b") });

        Assert.IsNull(framing.KnownLength);
        Assert.IsTrue(framing.IsChunked);
        Assert.IsTrue(framing.AddsExpect);
        Assert.IsTrue(framing.AwaitsContinue);
    }

    [TestMethod]
    [DataRow("Expect: 100-continue", 3, true, DisplayName = "-H Expect: 100-continue waits for a small body")]
    [DataRow("expect:   100-CONTINUE  ", 3, true, DisplayName = "-H Expect value compared without case or blanks")]
    [DataRow("Expect:", 1048577, false, DisplayName = "-H Expect: removes the wait")]
    [DataRow("Expect: other", 1048577, false, DisplayName = "-H Expect: other does not wait")]
    [DataRow("Expect;", 1048577, false, DisplayName = "-H Expect; sends it empty and does not wait")]
    public void Of_CustomExpect_ReplacesCurlsOwn(string header, int length, bool awaits)
    {
        HttpRequestFraming framing = Of(new HttpRequestOptions { Headers = [header], Body = new BytesBody(new byte[length], "a/b") });

        Assert.IsFalse(framing.AddsExpect);
        Assert.AreEqual(awaits, framing.AwaitsContinue);
    }

    [TestMethod]
    [DataRow("Transfer-Encoding: chunked", true, DisplayName = "chunked")]
    [DataRow("transfer-encoding: gzip, CHUNKED", true, DisplayName = "chunked in a list, any case")]
    [DataRow("Transfer-Encoding: gzip", false, DisplayName = "another coding")]
    [DataRow("Transfer-Encoding:", false, DisplayName = "removed")]
    [DataRow("X-A: chunked", false, DisplayName = "another header")]
    public void Of_CustomTransferEncoding_ChunksWhenItNamesChunked(string header, bool chunked)
    {
        HttpRequestFraming framing = Of(new HttpRequestOptions { Headers = [header], Body = new BytesBody("x=1"u8.ToArray(), "a/b") });

        Assert.AreEqual(chunked, framing.IsChunked);
        Assert.AreEqual(3L, framing.KnownLength);
    }

    private static HttpRequestFraming Of(HttpRequestOptions options) =>
        HttpRequestFraming.Of(options, [.. options.Headers.Select(HttpCustomHeader.Parse)]);
}
