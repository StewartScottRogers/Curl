using System.Globalization;
using Curl.Extension.OpenTelemetry.Otlp;
using Curl.Extension.OpenTelemetry.TraceContext;

namespace Curl.Extension.OpenTelemetry.UnitTests.Otlp;

[TestClass]
public sealed class OtlpHttpClientSpanTests
{
    private static readonly TraceParent Sent = new("4bf92f3577b34da6a3ce929d0e0e4736", "00f067aa0ba902b7", "01");
    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch.AddSeconds(1);

    [TestMethod]
    public void For_ASuccessfulTransfer_GivesAClientSpanWithTheHttpAttributes_SemanticConventionsHttpClientSpan()
    {
        var result = TransferResult.Success(5) with { Report = new TransferReport { Method = "POST", ResponseCode = 201 } };

        var span = OtlpHttpClientSpan.For(Sent, Context("https://user:secret@example.com:8443/a?b=c"), result, Start, Start.AddMilliseconds(1));

        Assert.AreEqual(new OtlpSpan(Sent.TraceId, Sent.ParentId, "POST", 1_000_000_000, 1_001_000_000, span.Attributes, false, null), span);
        CollectionAssert.AreEqual(
            new[]
            {
                OtlpAttribute.String("http.request.method", "POST"),
                OtlpAttribute.String("url.full", "https://REDACTED:REDACTED@example.com:8443/a?b=c"),
                OtlpAttribute.String("server.address", "example.com"),
                OtlpAttribute.Int("server.port", 8443),
                OtlpAttribute.Int("http.response.status_code", 201),
            },
            span.Attributes.ToArray());
    }

    [TestMethod]
    [DataRow(400, DisplayName = "400, the boundary")]
    [DataRow(404, DisplayName = "404")]
    public void For_AStatusOf400OrMore_IsAnErrorWithTheStatusAsItsType_SemanticConventionsHttpClientStatus(int statusCode)
    {
        var result = TransferResult.Success(0) with { Report = new TransferReport { ResponseCode = statusCode } };

        var span = OtlpHttpClientSpan.For(Sent, Context("http://example.com/"), result, Start, Start);

        Assert.IsTrue(span.IsError);
        Assert.AreEqual(OtlpAttribute.String("error.type", statusCode.ToString(CultureInfo.InvariantCulture)), span.Attributes[^1]);
        Assert.AreEqual("GET", span.Name);
    }

    [TestMethod]
    public void For_AFailedTransfer_IsAnErrorWithTheExitCodeAsItsTypeAndTheErrorAsItsMessage()
    {
        var context = new TransferContext { Url = CurlUrl.Parse("http://example.com/"), Output = Stream.Null, Http = new HttpRequestOptions { CustomMethod = "PURGE" } };

        var span = OtlpHttpClientSpan.For(Sent, context, TransferResult.Failure(CurlExitCode.CouldntResolveHost, "Could not resolve host: example.com"), Start, Start);

        Assert.IsTrue(span.IsError);
        Assert.AreEqual("PURGE", span.Name);
        Assert.AreEqual("Could not resolve host: example.com", span.StatusMessage);
        Assert.AreEqual(OtlpAttribute.String("error.type", nameof(CurlExitCode.CouldntResolveHost)), span.Attributes[^1]);
        Assert.HasCount(5, span.Attributes);
    }

    [TestMethod]
    [DataRow("http://example.com/a@b", "http://example.com/a@b", DisplayName = "an @ in the path")]
    [DataRow("http://u@example.com", "http://REDACTED:REDACTED@example.com", DisplayName = "a user and no path")]
    [DataRow("example.com/x", "example.com/x", DisplayName = "no scheme")]
    public void Redacted_RedactsOnlyTheUserInformation_SemanticConventionsUrlFull(string url, string expected)
    {
        Assert.AreEqual(expected, OtlpHttpClientSpan.Redacted(url));
    }

    private static TransferContext Context(string url) =>
        new() { Url = CurlUrl.Parse(url), Output = Stream.Null, Http = new HttpRequestOptions() };
}
