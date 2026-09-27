using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpResponseBodyFraming" /> to how curl 8.21.0 ended each body measured in
/// the BL-180 Notes, by default, for <c>--raw</c> and for <c>--ignore-content-length</c>.
/// </summary>
[TestClass]
public sealed class HttpResponseBodyFramingTests
{
    [TestMethod]
    [DataRow("chunked", "3", false, false, true, null, DisplayName = "Chunked wins over Content-Length")]
    [DataRow(null, "3", false, false, false, 3L, DisplayName = "Content-Length")]
    [DataRow(null, null, false, false, false, null, DisplayName = "Neither runs to close")]
    [DataRow("chunked", "3", true, false, false, null, DisplayName = "--raw chunked runs to close")]
    [DataRow("gzip, chunked", null, true, false, false, null, DisplayName = "--raw refuses no coding")]
    [DataRow(null, "3", true, false, false, 3L, DisplayName = "--raw keeps Content-Length")]
    [DataRow(null, "3", false, true, false, null, DisplayName = "--ignore-content-length runs to close")]
    [DataRow("chunked", "3", false, true, true, null, DisplayName = "--ignore-content-length still decodes chunked")]
    [DataRow(null, "abc", false, true, false, null, DisplayName = "--ignore-content-length reads no Content-Length")]
    public void Of_Headers_DecidesHowTheBodyEnds(
        string? transferEncoding,
        string? contentLength,
        bool passesTransferCoding,
        bool ignoresContentLength,
        bool isChunked,
        long? expectedLength)
    {
        HttpResponseBodyFraming framing = HttpResponseBodyFraming.Of(
            Headers(transferEncoding, contentLength),
            passesTransferCoding,
            ignoresContentLength);

        Assert.AreEqual(isChunked, framing.IsChunked);
        Assert.AreEqual(expectedLength, framing.ContentLength);
        Assert.AreEqual(!isChunked && expectedLength is null, framing.RunsToClose);
    }

    [TestMethod]
    public void Of_InvalidContentLength_ThrowsExit8()
    {
        HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpResponseBodyFraming.Of(Headers(null, "abc"), passesTransferCoding: true, ignoresContentLength: false));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, thrown.ExitCode);
    }

    [TestMethod]
    public void Of_UnsolicitedCodingWithoutRaw_ThrowsExit61() =>
        Assert.ThrowsExactly<HttpTransferException>(
            () => HttpResponseBodyFraming.Of(Headers("gzip", null), passesTransferCoding: false, ignoresContentLength: true));

    [TestMethod]
    [DataRow("gzip", "3", false, false, false, null, "gzip", DisplayName = "--tr-encoding gzip runs to close")]
    [DataRow("gzip, chunked", "3", false, false, true, null, "gzip", DisplayName = "--tr-encoding gzip, chunked")]
    [DataRow(null, "3", false, false, false, 3L, "", DisplayName = "--tr-encoding without Transfer-Encoding keeps Content-Length")]
    [DataRow(null, "3", false, true, false, null, "", DisplayName = "--tr-encoding with --ignore-content-length")]
    [DataRow("gzip, chunked", "3", true, false, false, null, "gzip", DisplayName = "--tr-encoding --raw passes chunked")]
    [DataRow("chunked, chunked", null, false, false, true, null, "", DisplayName = "--tr-encoding ignores a repeated chunked")]
    public void Of_TransferEncoding_DecidesHowTheBodyEndsAndWhatToDecode(
        string? transferEncoding,
        string? contentLength,
        bool passesTransferCoding,
        bool ignoresContentLength,
        bool isChunked,
        long? expectedLength,
        string codings)
    {
        HttpResponseBodyFraming framing = HttpResponseBodyFraming.Of(
            Headers(transferEncoding, contentLength),
            passesTransferCoding,
            ignoresContentLength,
            decodesTransferCoding: true);

        Assert.AreEqual(isChunked, framing.IsChunked);
        Assert.AreEqual(expectedLength, framing.ContentLength);
        Assert.AreEqual(codings, string.Join(", ", framing.TransferCodings));
    }

    [TestMethod]
    public void Of_TransferEncodingAfterAnInvalidContentLength_ThrowsExit8()
    {
        HttpResponseHeader[] headers = [new("Content-Length", "abc"), new("Transfer-Encoding", "gzip")];

        HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpResponseBodyFraming.Of(headers, passesTransferCoding: false, ignoresContentLength: false, decodesTransferCoding: true));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, thrown.ExitCode);
    }

    [TestMethod]
    public void Of_InvalidContentLengthAfterTransferEncoding_IsNotRead()
    {
        HttpResponseHeader[] headers = [new("Transfer-Encoding", "gzip"), new("Content-Length", "abc")];

        HttpResponseBodyFraming framing = HttpResponseBodyFraming.Of(headers, passesTransferCoding: false, ignoresContentLength: false, decodesTransferCoding: true);

        Assert.IsTrue(framing.RunsToClose);
    }

    [TestMethod]
    public void Of_RefusedTransferEncodingAfterAnInvalidContentLength_ThrowsExit8()
    {
        HttpResponseHeader[] headers = [new("Content-Length", "abc"), new("Transfer-Encoding", "chunked, gzip")];

        HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpResponseBodyFraming.Of(headers, passesTransferCoding: false, ignoresContentLength: true, decodesTransferCoding: true));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, thrown.ExitCode);
    }

    private static HttpResponseHeader[] Headers(string? transferEncoding, string? contentLength) =>
        [
            .. transferEncoding is null ? [] : new[] { new HttpResponseHeader("Transfer-Encoding", transferEncoding) },
            .. contentLength is null ? [] : new[] { new HttpResponseHeader("Content-Length", contentLength) },
        ];
}
