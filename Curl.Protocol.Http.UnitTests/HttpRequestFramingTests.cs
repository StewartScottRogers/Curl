using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpRequestFraming" />: the method, chunking and <c>100 Continue</c> wait
/// curl 8.21.0 chose for the requests measured in the BL-175 Notes.
/// </summary>
[TestClass]
public sealed class HttpRequestFramingTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Of_NoBody_IsGetWithNothingToFrame()
    {
        HttpRequestFraming framing = Of(new HttpRequestOptions());

        Diagnostics.Assert("framing.Method", "GET", framing.Method);
        Assert.AreEqual("GET", framing.Method);
        Diagnostics.Assert("framing.Body", null, framing.Body);
        Assert.IsNull(framing.Body);
        Diagnostics.Assert("framing.KnownLength", null, framing.KnownLength);
        Assert.IsNull(framing.KnownLength);
        Diagnostics.Assert("framing.IsChunked", false, framing.IsChunked);
        Assert.IsFalse(framing.IsChunked);
        Diagnostics.Assert("framing.AddsExpect", false, framing.AddsExpect);
        Assert.IsFalse(framing.AddsExpect);
        Diagnostics.Assert("framing.AwaitsContinue", false, framing.AwaitsContinue);
        Assert.IsFalse(framing.AwaitsContinue);
    }

    [TestMethod]
    public void Of_SmallBody_IsPostWithContentLengthAndNoWait()
    {
        BytesBody body = new("x=1"u8.ToArray(), "application/x-www-form-urlencoded");

        HttpRequestFraming framing = Of(new HttpRequestOptions { Body = body });

        Diagnostics.Assert("framing.Method", "POST", framing.Method);
        Assert.AreEqual("POST", framing.Method);
        Diagnostics.Assert("framing.Body is the same object", true, ReferenceEquals(body, framing.Body));
        Assert.AreSame(body, framing.Body);
        Diagnostics.Assert("framing.KnownLength", 3L, framing.KnownLength);
        Assert.AreEqual(3L, framing.KnownLength);
        Diagnostics.Assert("framing.IsChunked", false, framing.IsChunked);
        Assert.IsFalse(framing.IsChunked);
        Diagnostics.Assert("framing.AddsExpect", false, framing.AddsExpect);
        Assert.IsFalse(framing.AddsExpect);
        Diagnostics.Assert("framing.AwaitsContinue", false, framing.AwaitsContinue);
        Assert.IsFalse(framing.AwaitsContinue);
    }

    [TestMethod]
    public void Of_FileUpload_IsPutWithWhatIsLeftOfTheFileAsItsLength()
    {
        // curl -T f.txt (5 bytes) sent PUT with Content-Length: 5 and no Expect (BL-184 Notes).
        MemoryStream upload = new("xxhello"u8.ToArray()) { Position = 2 };

        HttpRequestFraming framing = FramingOf(new HttpRequestOptions(), [], upload: upload);

        Diagnostics.Assert("framing.Method", "PUT", framing.Method);
        Assert.AreEqual("PUT", framing.Method);
        Diagnostics.Assert("((StreamBody)framing.Body!).Content is the same object", true, ReferenceEquals(upload, ((StreamBody)framing.Body!).Content));
        Assert.AreSame(upload, ((StreamBody)framing.Body!).Content);
        Diagnostics.Assert("framing.KnownLength", 5L, framing.KnownLength);
        Assert.AreEqual(5L, framing.KnownLength);
        Diagnostics.Assert("framing.IsUpload", true, framing.IsUpload);
        Assert.IsTrue(framing.IsUpload);
        Diagnostics.Assert("framing.IsChunked", false, framing.IsChunked);
        Assert.IsFalse(framing.IsChunked);
        Diagnostics.Assert("framing.AddsExpect", false, framing.AddsExpect);
        Assert.IsFalse(framing.AddsExpect);
    }

    [TestMethod]
    public void Of_StandardInputUpload_IsChunkedAndWaitsForContinue()
    {
        // curl -T - sent Transfer-Encoding: chunked and Expect: 100-continue (BL-184 Notes).
        HttpRequestFraming framing = FramingOf(
            new HttpRequestOptions(),
            [],
            upload: new FailingReadStream([], 1, new IOException("End.")));

        Diagnostics.Assert("framing.Method", "PUT", framing.Method);
        Assert.AreEqual("PUT", framing.Method);
        Diagnostics.Assert("framing.KnownLength", null, framing.KnownLength);
        Assert.IsNull(framing.KnownLength);
        Diagnostics.Assert("framing.IsChunked", true, framing.IsChunked);
        Assert.IsTrue(framing.IsChunked);
        Diagnostics.Assert("framing.AddsExpect", true, framing.AddsExpect);
        Assert.IsTrue(framing.AddsExpect);
        Diagnostics.Assert("framing.AwaitsContinue", true, framing.AwaitsContinue);
        Assert.IsTrue(framing.AwaitsContinue);
        Diagnostics.Assert("framing.WithoutExpect(framing.Body).IsUpload", true, framing.WithoutExpect(framing.Body).IsUpload);
        Assert.IsTrue(framing.WithoutExpect(framing.Body).IsUpload);
    }

    [TestMethod]
    public void ForHttp2OrHttp3_UploadOfUnknownLength_IsNeitherChunkedNorWaitingForContinue()
    {
        HttpRequestFraming framing = FramingOf(new HttpRequestOptions(), [], upload: new UnseekableStream([]));

        HttpRequestFraming http2 = framing.ForHttp2OrHttp3();

        Diagnostics.Assert("framing.IsChunked", true, framing.IsChunked);
        Assert.IsTrue(framing.IsChunked);
        Diagnostics.Assert("framing.AddsExpect", true, framing.AddsExpect);
        Assert.IsTrue(framing.AddsExpect);
        Diagnostics.Assert("http2.IsChunked", false, http2.IsChunked);
        Assert.IsFalse(http2.IsChunked);
        Diagnostics.Assert("http2.AddsExpect", false, http2.AddsExpect);
        Assert.IsFalse(http2.AddsExpect);
        Diagnostics.Assert("http2.AwaitsContinue", false, http2.AwaitsContinue);
        Assert.IsFalse(http2.AwaitsContinue);
        Diagnostics.Assert("http2.Method", "PUT", http2.Method);
        Assert.AreEqual("PUT", http2.Method);
        Diagnostics.Assert("http2.Body is the same object", true, ReferenceEquals(framing.Body, http2.Body));
        Assert.AreSame(framing.Body, http2.Body);
        Diagnostics.Assert("http2.KnownLength", null, http2.KnownLength);
        Assert.IsNull(http2.KnownLength);
        Diagnostics.Assert("http2.IsUpload", true, http2.IsUpload);
        Assert.IsTrue(http2.IsUpload);
    }

    [TestMethod]
    public void Of_UploadWithCustomMethodAndBody_SendsTheUploadWithTheCustomMethod()
    {
        HttpRequestOptions options = new() { CustomMethod = "POST", Body = new BytesBody("x"u8.ToArray(), "a/b") };

        HttpRequestFraming framing = FramingOf(options, [], upload: new MemoryStream([1, 2]));

        Diagnostics.Assert("framing.Method", "POST", framing.Method);
        Assert.AreEqual("POST", framing.Method);
        Diagnostics.Assert("framing.KnownLength", 2L, framing.KnownLength);
        Assert.AreEqual(2L, framing.KnownLength);
        Diagnostics.Assert("framing.IsUpload", true, framing.IsUpload);
        Assert.IsTrue(framing.IsUpload);
    }

    [TestMethod]
    public void Of_Body_IsNoUpload()
    {
        HttpRequestFraming framing = Of(new HttpRequestOptions { Body = new BytesBody("x"u8.ToArray(), "a/b") });

        Diagnostics.Assert("framing.IsUpload", false, framing.IsUpload);
        Assert.IsFalse(framing.IsUpload);
    }

    [TestMethod]
    [DataRow(null, "HEAD", DisplayName = "-I sends HEAD")]
    [DataRow("GET", "GET", DisplayName = "-I -X GET sends GET")]
    public void Of_NoBodyRequested_IsHeadUnlessCustomMethod(string? customMethod, string method)
    {
        HttpRequestOptions options = new() { CustomMethod = customMethod };

        HttpRequestFraming framing = FramingOf(options, [], noBody: true);

        Diagnostics.Assert("framing.Method", method, framing.Method);
        Assert.AreEqual(method, framing.Method);
        Diagnostics.Assert("framing.Body", null, framing.Body);
        Assert.IsNull(framing.Body);
    }

    [TestMethod]
    public void Of_NoBodyRequestedWithBody_StaysPost()
    {
        HttpRequestOptions options = new() { Body = new BytesBody("x"u8.ToArray(), "a/b") };

        HttpRequestFraming framing = FramingOf(options, [], noBody: true);

        Diagnostics.Assert("framing.Method", "POST", framing.Method);
        Assert.AreEqual("POST", framing.Method);
    }

    [TestMethod]
    public void Of_CustomMethodWithoutBody_KeepsTheMethod()
    {
        HttpRequestFraming framing = Of(new HttpRequestOptions { CustomMethod = "DELETE" });

        Diagnostics.Assert("framing.Method", "DELETE", framing.Method);
        Assert.AreEqual("DELETE", framing.Method);
    }

    [TestMethod]
    [DataRow(1048576L, false, DisplayName = "1 MiB")]
    [DataRow(1048577L, true, DisplayName = "1 MiB + 1")]
    public void Of_StreamOfKnownLength_ExpectsAboveOneMebibyte(long length, bool expects)
    {
        HttpRequestFraming framing = Of(new HttpRequestOptions { Body = new StreamBody(Stream.Null, length, "a/b") });

        Diagnostics.Assert("framing.KnownLength", length, framing.KnownLength);
        Assert.AreEqual(length, framing.KnownLength);
        Diagnostics.Assert("framing.IsChunked", false, framing.IsChunked);
        Assert.IsFalse(framing.IsChunked);
        Diagnostics.Assert("framing.AddsExpect", expects, framing.AddsExpect);
        Assert.AreEqual(expects, framing.AddsExpect);
        Diagnostics.Assert("framing.AwaitsContinue", expects, framing.AwaitsContinue);
        Assert.AreEqual(expects, framing.AwaitsContinue);
    }

    [TestMethod]
    public void Of_StreamOfUnknownLength_IsChunkedAndExpects()
    {
        HttpRequestFraming framing = Of(new HttpRequestOptions { Body = new StreamBody(Stream.Null, null, "a/b") });

        Diagnostics.Assert("framing.KnownLength", null, framing.KnownLength);
        Assert.IsNull(framing.KnownLength);
        Diagnostics.Assert("framing.IsChunked", true, framing.IsChunked);
        Assert.IsTrue(framing.IsChunked);
        Diagnostics.Assert("framing.AddsExpect", true, framing.AddsExpect);
        Assert.IsTrue(framing.AddsExpect);
        Diagnostics.Assert("framing.AwaitsContinue", true, framing.AwaitsContinue);
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

        Diagnostics.Assert("framing.AddsExpect", false, framing.AddsExpect);
        Assert.IsFalse(framing.AddsExpect);
        Diagnostics.Assert("framing.AwaitsContinue", awaits, framing.AwaitsContinue);
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

        Diagnostics.Assert("framing.IsChunked", chunked, framing.IsChunked);
        Assert.AreEqual(chunked, framing.IsChunked);
        Diagnostics.Assert("framing.KnownLength", 3L, framing.KnownLength);
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

        Diagnostics.Assert("framing.AddsExpect", false, framing.AddsExpect);
        Assert.IsFalse(framing.AddsExpect);
        Diagnostics.Assert("framing.AwaitsContinue", false, framing.AwaitsContinue);
        Assert.IsFalse(framing.AwaitsContinue);
        Diagnostics.Assert("framing.RefusesUnknownLength", false, framing.RefusesUnknownLength);
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

        Diagnostics.Assert("framing.AddsExpect", false, framing.AddsExpect);
        Assert.IsFalse(framing.AddsExpect);
        Diagnostics.Assert("framing.AwaitsContinue", true, framing.AwaitsContinue);
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

        Diagnostics.Assert("framing.RefusesUnknownLength", refused, framing.RefusesUnknownLength);
        Assert.AreEqual(refused, framing.RefusesUnknownLength);
        Diagnostics.Assert("framing.IsChunked", true, framing.IsChunked);
        Assert.IsTrue(framing.IsChunked);
    }

    [TestMethod]
    [DataRow("0-9", 1, "bytes 0-9/1", DisplayName = "-d x -r 0-9")]
    [DataRow("0-9", 5, "bytes 0-9/5", DisplayName = "-d hello -r 0-9")]
    [DataRow("0-9,20-29", 1, "bytes 0-9,20-29/1", DisplayName = "-d x -r 0-9,20-29")]
    public void Of_RangeOnADataBody_SendsTheRangeOverTheBodyLength(string rangeText, int length, string expected)
    {
        HttpRequestOptions options = new() { Body = new BytesBody(new byte[length], "a/b") };

        HttpRequestFraming framing = FramingOf(options, [], rangeText: rangeText);

        Diagnostics.Assert("framing.ContentRange", expected, framing.ContentRange);
        Assert.AreEqual(expected, framing.ContentRange);
        Diagnostics.Assert("framing.WithoutExpect(framing.Body).ContentRange", expected, framing.WithoutExpect(framing.Body).ContentRange);
        Assert.AreEqual(expected, framing.WithoutExpect(framing.Body).ContentRange);
    }

    [TestMethod]
    public void Of_NoRange_SendsNoContentRange()
    {
        HttpRequestFraming framing = Of(new HttpRequestOptions { Body = new BytesBody("x"u8.ToArray(), "a/b") });

        Diagnostics.Assert("framing.ContentRange", null, framing.ContentRange);
        Assert.IsNull(framing.ContentRange);
    }

    [TestMethod]
    public void Of_RangeOnAFormBody_SendsNoContentRange()
    {
        HttpRequestOptions options = new() { Body = new StreamBody(Stream.Null, 149, "multipart/form-data; boundary=b") };

        HttpRequestFraming framing = FramingOf(options, [], rangeText: "0-9");

        Diagnostics.Assert("framing.ContentRange", null, framing.ContentRange);
        Assert.IsNull(framing.ContentRange);
    }

    [TestMethod]
    public void Of_RangeWithoutABody_SendsNoContentRange()
    {
        HttpRequestFraming framing = FramingOf(new HttpRequestOptions(), [], rangeText: "0-9");

        Diagnostics.Assert("framing.ContentRange", null, framing.ContentRange);
        Assert.IsNull(framing.ContentRange);
    }

    [TestMethod]
    public void Of_RangeOnAnUpload_SendsTheRangeOverTheUploadLength()
    {
        HttpRequestFraming framing = FramingOf(new HttpRequestOptions(), [], upload: new MemoryStream(new byte[87]), rangeText: "0-9");

        Diagnostics.Assert("framing.ContentRange", "bytes 0-9/87", framing.ContentRange);
        Assert.AreEqual("bytes 0-9/87", framing.ContentRange);
    }

    [TestMethod]
    public void Of_RangeOnAnUploadOfUnknownLength_SendsMinusOneForTheLength()
    {
        using FailingReadStream upload = new([], 1, new IOException());

        HttpRequestFraming framing = FramingOf(new HttpRequestOptions(), [], upload: upload, rangeText: "0-9");

        Diagnostics.Assert("framing.ContentRange", "bytes 0-9/-1", framing.ContentRange);
        Assert.AreEqual("bytes 0-9/-1", framing.ContentRange);
    }

    [TestMethod]
    public void Of_ResumedUploadWithARange_SendsTheResumeContentRange()
    {
        HttpRequestFraming framing = FramingOf(new HttpRequestOptions(), [], upload: new MemoryStream(new byte[10]), resumeFrom: 4, rangeText: "0-1");

        Diagnostics.Assert("framing.ContentRange", "bytes 4-9/10", framing.ContentRange);
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

        Diagnostics.Assert("resent.AddsExpect", false, resent.AddsExpect);
        Assert.IsFalse(resent.AddsExpect);
        Diagnostics.Assert("resent.AwaitsContinue", expected, resent.AwaitsContinue);
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

        Diagnostics.Assert("framing.AwaitsContinue", ownExpect, framing.AwaitsContinue);
        Assert.AreEqual(ownExpect, framing.AwaitsContinue);
        Diagnostics.Assert("resent.AwaitsContinue", false, resent.AwaitsContinue);
        Assert.IsFalse(resent.AwaitsContinue);
    }

    private HttpRequestFraming Of(HttpRequestOptions options) =>
        FramingOf(options, [.. options.Headers.Select(HttpCustomHeader.Parse)]);

    private HttpRequestFraming FramingOf(HttpRequestOptions options, HttpCustomHeader[] customHeaders, bool noBody = false, Stream? upload = null, long? resumeFrom = null, string? rangeText = null)
    {
        Diagnostics.Arrange("options", HttpRequestOptionsDescription.Of(options));
        Diagnostics.Arrange(
            "custom headers, no body, upload, resume from, range",
            $"{customHeaders.Length}, {noBody}, {(upload is null ? "(none)" : upload.GetType().Name)}, {(resumeFrom is null ? "(none)" : resumeFrom)}, {rangeText ?? "(none)"}");

        HttpRequestFraming framing = HttpRequestFraming.Of(options, customHeaders, noBody, upload, resumeFrom, rangeText);

        Diagnostics.Act(
            "framing",
            $"method {framing.Method}, known length {(framing.KnownLength is null ? "(unknown)" : framing.KnownLength)}, chunked {framing.IsChunked}, "
                + $"adds Expect {framing.AddsExpect}, awaits continue {framing.AwaitsContinue}, upload {framing.IsUpload}, "
                + $"refuses unknown length {framing.RefusesUnknownLength}, Content-Range {framing.ContentRange ?? "(none)"}");
        return framing;
    }
}
