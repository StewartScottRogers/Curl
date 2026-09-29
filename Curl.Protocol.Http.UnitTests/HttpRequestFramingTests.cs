using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
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
    public void Of_FileUpload_IsPutWithWhatIsLeftOfTheFileAsItsLength()
    {
        // curl -T f.txt (5 bytes) sent PUT with Content-Length: 5 and no Expect (BL-184 Notes).
        MemoryStream upload = new("xxhello"u8.ToArray()) { Position = 2 };

        HttpRequestFraming framing = HttpRequestFraming.Of(new HttpRequestOptions(), [], upload: upload);

        Assert.AreEqual("PUT", framing.Method);
        Assert.AreSame(upload, ((StreamBody)framing.Body!).Content);
        Assert.AreEqual(5L, framing.KnownLength);
        Assert.IsTrue(framing.IsUpload);
        Assert.IsFalse(framing.IsChunked);
        Assert.IsFalse(framing.AddsExpect);
    }

    [TestMethod]
    public void Of_StandardInputUpload_IsChunkedAndWaitsForContinue()
    {
        // curl -T - sent Transfer-Encoding: chunked and Expect: 100-continue (BL-184 Notes).
        HttpRequestFraming framing = HttpRequestFraming.Of(
            new HttpRequestOptions(),
            [],
            upload: new FailingReadStream([], 1, new IOException("End.")));

        Assert.AreEqual("PUT", framing.Method);
        Assert.IsNull(framing.KnownLength);
        Assert.IsTrue(framing.IsChunked);
        Assert.IsTrue(framing.AddsExpect);
        Assert.IsTrue(framing.AwaitsContinue);
        Assert.IsTrue(framing.WithoutExpect(framing.Body).IsUpload);
    }

    [TestMethod]
    public void ForHttp2_UploadOfUnknownLength_IsNeitherChunkedNorWaitingForContinue()
    {
        HttpRequestFraming framing = HttpRequestFraming.Of(new HttpRequestOptions(), [], upload: new UnseekableStream([]));

        HttpRequestFraming http2 = framing.ForHttp2();

        Assert.IsTrue(framing.IsChunked);
        Assert.IsTrue(framing.AddsExpect);
        Assert.IsFalse(http2.IsChunked);
        Assert.IsFalse(http2.AddsExpect);
        Assert.IsFalse(http2.AwaitsContinue);
        Assert.AreEqual("PUT", http2.Method);
        Assert.AreSame(framing.Body, http2.Body);
        Assert.IsNull(http2.KnownLength);
        Assert.IsTrue(http2.IsUpload);
    }

    [TestMethod]
    public void Of_UploadWithCustomMethodAndBody_SendsTheUploadWithTheCustomMethod()
    {
        HttpRequestOptions options = new() { CustomMethod = "POST", Body = new BytesBody("x"u8.ToArray(), "a/b") };

        HttpRequestFraming framing = HttpRequestFraming.Of(options, [], upload: new MemoryStream([1, 2]));

        Assert.AreEqual("POST", framing.Method);
        Assert.AreEqual(2L, framing.KnownLength);
        Assert.IsTrue(framing.IsUpload);
    }

    [TestMethod]
    public void Of_Body_IsNoUpload()
    {
        Assert.IsFalse(Of(new HttpRequestOptions { Body = new BytesBody("x"u8.ToArray(), "a/b") }).IsUpload);
    }

    [TestMethod]
    [DataRow(null, "HEAD", DisplayName = "-I sends HEAD")]
    [DataRow("GET", "GET", DisplayName = "-I -X GET sends GET")]
    public void Of_NoBodyRequested_IsHeadUnlessCustomMethod(string? customMethod, string method)
    {
        HttpRequestOptions options = new() { CustomMethod = customMethod };

        HttpRequestFraming framing = HttpRequestFraming.Of(options, [], noBody: true);

        Assert.AreEqual(method, framing.Method);
        Assert.IsNull(framing.Body);
    }

    [TestMethod]
    public void Of_NoBodyRequestedWithBody_StaysPost()
    {
        HttpRequestOptions options = new() { Body = new BytesBody("x"u8.ToArray(), "a/b") };

        Assert.AreEqual("POST", HttpRequestFraming.Of(options, [], noBody: true).Method);
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

    [TestMethod]
    public void Of_Http10BodyAboveTheThreshold_AddsNoExpect()
    {
        HttpRequestOptions options = new()
        {
            Version = HttpVersionPreference.Http10,
            Body = new BytesBody(new byte[HttpRequestFraming.ExpectContinueThreshold + 1], "a/b"),
        };

        HttpRequestFraming framing = Of(options);

        Assert.IsFalse(framing.AddsExpect);
        Assert.IsFalse(framing.AwaitsContinue);
        Assert.IsFalse(framing.RefusesUnknownLength);
    }

    [TestMethod]
    public void Of_Http10WithExpectHeader_StillWaitsForContinue()
    {
        HttpRequestOptions options = new()
        {
            Version = HttpVersionPreference.Http10,
            Headers = ["Expect: 100-continue"],
            Body = new BytesBody("x"u8.ToArray(), "a/b"),
        };

        HttpRequestFraming framing = Of(options);

        Assert.IsFalse(framing.AddsExpect);
        Assert.IsTrue(framing.AwaitsContinue);
    }

    [TestMethod]
    [DataRow(HttpVersionPreference.Http10, null, true, DisplayName = "-0 refuses an unknown length")]
    [DataRow(HttpVersionPreference.Http10, "Transfer-Encoding: chunked", false, DisplayName = "-0 with -H chunked sends it chunked")]
    [DataRow(HttpVersionPreference.Http11, null, false, DisplayName = "HTTP/1.1 sends it chunked")]
    public void Of_BodyOfUnknownLength_IsRefusedOnlyOverHttp10WithoutChunkedHeader(HttpVersionPreference version, string? header, bool refused)
    {
        HttpRequestOptions options = new()
        {
            Version = version,
            Headers = header is null ? [] : [header],
            Body = new StreamBody(Stream.Null, null, "a/b"),
        };

        HttpRequestFraming framing = Of(options);

        Assert.AreEqual(refused, framing.RefusesUnknownLength);
        Assert.IsTrue(framing.IsChunked);
    }

    [TestMethod]
    [DataRow("0-9", 1, "bytes 0-9/1", DisplayName = "-d x -r 0-9")]
    [DataRow("0-9", 5, "bytes 0-9/5", DisplayName = "-d hello -r 0-9")]
    [DataRow("0-9,20-29", 1, "bytes 0-9,20-29/1", DisplayName = "-d x -r 0-9,20-29")]
    public void Of_RangeOnADataBody_SendsTheRangeOverTheBodyLength(string rangeText, int length, string expected)
    {
        HttpRequestOptions options = new() { Body = new BytesBody(new byte[length], "a/b") };

        HttpRequestFraming framing = HttpRequestFraming.Of(options, [], rangeText: rangeText);

        Assert.AreEqual(expected, framing.ContentRange);
        Assert.AreEqual(expected, framing.WithoutExpect(framing.Body).ContentRange);
    }

    [TestMethod]
    public void Of_NoRange_SendsNoContentRange() =>
        Assert.IsNull(Of(new HttpRequestOptions { Body = new BytesBody("x"u8.ToArray(), "a/b") }).ContentRange);

    [TestMethod]
    public void Of_RangeOnAFormBody_SendsNoContentRange()
    {
        HttpRequestOptions options = new() { Body = new StreamBody(Stream.Null, 149, "multipart/form-data; boundary=b") };

        Assert.IsNull(HttpRequestFraming.Of(options, [], rangeText: "0-9").ContentRange);
    }

    [TestMethod]
    public void Of_RangeWithoutABody_SendsNoContentRange() =>
        Assert.IsNull(HttpRequestFraming.Of(new HttpRequestOptions(), [], rangeText: "0-9").ContentRange);

    [TestMethod]
    public void Of_RangeOnAnUpload_SendsTheRangeOverTheUploadLength()
    {
        HttpRequestFraming framing = HttpRequestFraming.Of(new HttpRequestOptions(), [], upload: new MemoryStream(new byte[87]), rangeText: "0-9");

        Assert.AreEqual("bytes 0-9/87", framing.ContentRange);
    }

    [TestMethod]
    public void Of_RangeOnAnUploadOfUnknownLength_SendsMinusOneForTheLength()
    {
        using FailingReadStream upload = new([], 1, new IOException());

        HttpRequestFraming framing = HttpRequestFraming.Of(new HttpRequestOptions(), [], upload: upload, rangeText: "0-9");

        Assert.AreEqual("bytes 0-9/-1", framing.ContentRange);
    }

    [TestMethod]
    public void Of_ResumedUploadWithARange_SendsTheResumeContentRange()
    {
        HttpRequestFraming framing = HttpRequestFraming.Of(new HttpRequestOptions(), [], upload: new MemoryStream(new byte[10]), resumeFrom: 4, rangeText: "0-1");

        Assert.AreEqual("bytes 4-9/10", framing.ContentRange);
    }

    [TestMethod]
    [DataRow(false, false, DisplayName = "-H Expect, 417 during the wait")]
    [DataRow(true, true, DisplayName = "-H Expect, 417 while sending")]
    public void WithoutExpect_CustomExpect_KeepsTheWaitOnlyWhenAsked(bool keepsCustomWait, bool expected)
    {
        // A resend after a 417 while sending waits again for an -H Expect (BL-396 Notes);
        // one after a 417 during the wait does not (BL-260 Notes).
        HttpRequestFraming framing = Of(new HttpRequestOptions { Headers = ["Expect: 100-continue"], Body = new BytesBody("hi"u8.ToArray(), "a/b") });

        HttpRequestFraming resent = framing.WithoutExpect(framing.Body, keepsCustomWait);

        Assert.IsFalse(resent.AddsExpect);
        Assert.AreEqual(expected, resent.AwaitsContinue);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "curl's own Expect")]
    [DataRow(false, DisplayName = "small body, no wait to keep")]
    public void WithoutExpect_KeepingTheCustomWait_NeverWaitsWithoutAnHExpect(bool ownExpect)
    {
        // curl's own Expect is dropped with its wait; a small body never waited.
        byte[] content = ownExpect ? new byte[HttpRequestFraming.ExpectContinueThreshold + 1] : "hi"u8.ToArray();
        HttpRequestFraming framing = Of(new HttpRequestOptions { Body = new BytesBody(content, "a/b") });

        HttpRequestFraming resent = framing.WithoutExpect(framing.Body, keepsCustomWait: true);

        Assert.AreEqual(ownExpect, framing.AwaitsContinue);
        Assert.IsFalse(resent.AwaitsContinue);
    }

    private static HttpRequestFraming Of(HttpRequestOptions options) =>
        HttpRequestFraming.Of(options, [.. options.Headers.Select(HttpCustomHeader.Parse)]);
}
