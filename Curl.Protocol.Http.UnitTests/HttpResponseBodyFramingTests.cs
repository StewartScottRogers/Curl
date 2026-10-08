using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpResponseBodyFraming" /> to how curl 8.21.0 ended each body measured in
/// the BL-180 Notes, by default, for <c>--raw</c> and for <c>--ignore-content-length</c>.
/// </summary>
[TestClass]
public sealed class HttpResponseBodyFramingTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

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
        Diagnostics.Arrange("Transfer-Encoding", transferEncoding ?? "(none)");
        Diagnostics.Arrange("Content-Length", contentLength ?? "(none)");
        Diagnostics.Arrange("passesTransferCoding", passesTransferCoding);
        Diagnostics.Arrange("ignoresContentLength", ignoresContentLength);

        HttpResponseBodyFraming framing = HttpResponseBodyFraming.Of(
            Headers(transferEncoding, contentLength),
            passesTransferCoding,
            ignoresContentLength);

        Diagnostics.Act("IsChunked", framing.IsChunked);
        Diagnostics.Act("ContentLength", framing.ContentLength?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)");
        Diagnostics.Act("RunsToClose", framing.RunsToClose);
        Diagnostics.Assert("IsChunked", isChunked, framing.IsChunked);
        Diagnostics.Assert("ContentLength", expectedLength?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)", framing.ContentLength?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)");
        Diagnostics.Assert("RunsToClose", !isChunked && expectedLength is null, framing.RunsToClose);
        Assert.AreEqual(isChunked, framing.IsChunked);
        Assert.AreEqual(expectedLength, framing.ContentLength);
        Assert.AreEqual(!isChunked && expectedLength is null, framing.RunsToClose);
    }

    [TestMethod]
    public void Of_InvalidContentLength_ThrowsExit8()
    {
        Diagnostics.Arrange("Content-Length", "abc");

        HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpResponseBodyFraming.Of(Headers(null, "abc"), passesTransferCoding: true, ignoresContentLength: false));

        Diagnostics.Act("exception", $"{thrown.GetType().Name}, exit {(int)thrown.ExitCode} ({thrown.ExitCode}): {thrown.Message}");
        Diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, thrown.ExitCode);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, thrown.ExitCode);
    }

    [TestMethod]
    public void Of_UnsolicitedCodingWithoutRaw_ThrowsExit61()
    {
        Diagnostics.Arrange("Transfer-Encoding", "gzip");

        HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpResponseBodyFraming.Of(Headers("gzip", null), passesTransferCoding: false, ignoresContentLength: true));

        Diagnostics.Act("exception", $"{thrown.GetType().Name}, exit {(int)thrown.ExitCode} ({thrown.ExitCode}): {thrown.Message}");
        Diagnostics.Assert("exception type", typeof(HttpTransferException).Name, thrown.GetType().Name);
    }

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
        Diagnostics.Arrange("Transfer-Encoding", transferEncoding ?? "(none)");
        Diagnostics.Arrange("Content-Length", contentLength ?? "(none)");
        Diagnostics.Arrange("passesTransferCoding", passesTransferCoding);
        Diagnostics.Arrange("ignoresContentLength", ignoresContentLength);

        HttpResponseBodyFraming framing = HttpResponseBodyFraming.Of(
            Headers(transferEncoding, contentLength),
            passesTransferCoding,
            ignoresContentLength,
            decodesTransferCoding: true);

        Diagnostics.Act("IsChunked", framing.IsChunked);
        Diagnostics.Act("ContentLength", framing.ContentLength?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)");
        Diagnostics.Act("TransferCodings", string.Join(", ", framing.TransferCodings));
        Diagnostics.Assert("IsChunked", isChunked, framing.IsChunked);
        Diagnostics.Assert("ContentLength", expectedLength?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)", framing.ContentLength?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)");
        Diagnostics.Assert("TransferCodings", codings, string.Join(", ", framing.TransferCodings));
        Assert.AreEqual(isChunked, framing.IsChunked);
        Assert.AreEqual(expectedLength, framing.ContentLength);
        Assert.AreEqual(codings, string.Join(", ", framing.TransferCodings));
    }

    [TestMethod]
    public void Of_TransferEncodingAfterAnInvalidContentLength_ThrowsExit8()
    {
        HttpResponseHeader[] headers = [new("Content-Length", "abc"), new("Transfer-Encoding", "gzip")];
        Diagnostics.Arrange("headers", "Content-Length: abc; Transfer-Encoding: gzip");

        HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpResponseBodyFraming.Of(headers, passesTransferCoding: false, ignoresContentLength: false, decodesTransferCoding: true));

        Diagnostics.Act("exception", $"{thrown.GetType().Name}, exit {(int)thrown.ExitCode} ({thrown.ExitCode}): {thrown.Message}");
        Diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, thrown.ExitCode);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, thrown.ExitCode);
    }

    [TestMethod]
    public void Of_InvalidContentLengthAfterTransferEncoding_ThrowsExit8()
    {
        HttpResponseHeader[] headers = [new("Transfer-Encoding", "gzip"), new("Content-Length", "abc")];
        Diagnostics.Arrange("headers", "Transfer-Encoding: gzip; Content-Length: abc");

        HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpResponseBodyFraming.Of(headers, passesTransferCoding: false, ignoresContentLength: false, decodesTransferCoding: true));

        Diagnostics.Act("exception", $"{thrown.GetType().Name}, exit {(int)thrown.ExitCode} ({thrown.ExitCode}): {thrown.Message}");
        Diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, thrown.ExitCode);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, thrown.ExitCode);
    }

    [TestMethod]
    public void Of_ValidContentLengthAfterTransferEncoding_IsNotTrusted()
    {
        HttpResponseHeader[] headers = [new("Transfer-Encoding", "gzip"), new("Content-Length", "3")];
        Diagnostics.Arrange("headers", "Transfer-Encoding: gzip; Content-Length: 3");

        HttpResponseBodyFraming framing = HttpResponseBodyFraming.Of(headers, passesTransferCoding: false, ignoresContentLength: false, decodesTransferCoding: true);

        Diagnostics.Act("RunsToClose", framing.RunsToClose);
        Diagnostics.Assert("RunsToClose", true, framing.RunsToClose);
        Assert.IsTrue(framing.RunsToClose);
    }

    [TestMethod]
    public void Of_RefusedTransferEncodingAfterAnInvalidContentLength_ThrowsExit8()
    {
        HttpResponseHeader[] headers = [new("Content-Length", "abc"), new("Transfer-Encoding", "chunked, gzip")];
        Diagnostics.Arrange("headers", "Content-Length: abc; Transfer-Encoding: chunked, gzip");

        HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpResponseBodyFraming.Of(headers, passesTransferCoding: false, ignoresContentLength: true, decodesTransferCoding: true));

        Diagnostics.Act("exception", $"{thrown.GetType().Name}, exit {(int)thrown.ExitCode} ({thrown.ExitCode}): {thrown.Message}");
        Diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, thrown.ExitCode);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, thrown.ExitCode);
    }

    private static HttpResponseHeader[] Headers(string? transferEncoding, string? contentLength) =>
        [
            .. transferEncoding is null ? [] : new[] { new HttpResponseHeader("Transfer-Encoding", transferEncoding) },
            .. contentLength is null ? [] : new[] { new HttpResponseHeader("Content-Length", contentLength) },
        ];
}
