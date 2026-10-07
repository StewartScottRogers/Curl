using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins that an <see cref="AwsSigV4Inputs" /> carries what <c>--aws-sigv4</c> signs, with no body,
/// no upload and no <c>--path-as-is</c> by default, and that an <see cref="HttpAuthRequest" />
/// carries none unless given (BL-629).
/// </summary>
[TestClass]
public sealed class AwsSigV4InputsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_RoundTripsTheParameterHostAndHeadersWithDefaultsForTheRest()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string[] headers = ["X-A: 1"];
        diagnostics.Arrange("parameter", "aws:amz:us-east-1:s3");
        diagnostics.Arrange("host header", "127.0.0.1:18629");
        diagnostics.Arrange("headers", string.Join("|", headers));

        AwsSigV4Inputs inputs = new("aws:amz:us-east-1:s3", "127.0.0.1:18629", headers);

        diagnostics.Act("parameter", inputs.Parameter);
        diagnostics.Act("host header", inputs.HostHeaderValue);
        diagnostics.Act("post fields", inputs.PostFields);
        diagnostics.Act("upload size", inputs.UploadSize);
        diagnostics.Act("is get or head", inputs.IsGetOrHead);
        diagnostics.Act("path as is", inputs.PathAsIs);
        diagnostics.Assert("upload size", -1, inputs.UploadSize);
        Assert.AreEqual("aws:amz:us-east-1:s3", inputs.Parameter);
        Assert.AreEqual("127.0.0.1:18629", inputs.HostHeaderValue);
        Assert.AreSame(headers, inputs.CustomHeaders);
        Assert.IsNull(inputs.PostFields);
        Assert.AreEqual(-1, inputs.UploadSize);
        Assert.IsFalse(inputs.IsGetOrHead);
        Assert.IsFalse(inputs.PathAsIs);
    }

    [TestMethod]
    public void Init_EveryOptionalMemberSet_RoundTripsEveryValue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] body = [0x61];
        diagnostics.Bytes("post fields", body);
        diagnostics.Arrange("upload size", 12);

        AwsSigV4Inputs inputs = new AwsSigV4Inputs("osc", "h", []) with
        {
            PostFields = body,
            UploadSize = 12,
            IsGetOrHead = true,
            PathAsIs = true,
        };

        byte[] actualBody = inputs.PostFields!.Value.ToArray();
        diagnostics.Act("upload size", inputs.UploadSize);
        diagnostics.Act("is get or head", inputs.IsGetOrHead);
        diagnostics.Act("path as is", inputs.PathAsIs);
        diagnostics.Diff("post fields", body, actualBody);
        diagnostics.Assert("upload size", 12, inputs.UploadSize);
        CollectionAssert.AreEqual(body, actualBody);
        Assert.AreEqual(12, inputs.UploadSize);
        Assert.IsTrue(inputs.IsGetOrHead);
        Assert.IsTrue(inputs.PathAsIs);
    }

    [TestMethod]
    public void HttpAuthRequestAwsSigV4_NullByDefaultAndKeptWhenSet()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        HttpAuthRequest request = new("GET", CurlUrl.Parse("http://example.com/"), "/", null, null, HttpAuthSchemes.Basic, IsProxy: false);
        AwsSigV4Inputs inputs = new("aws", "example.com", []);
        diagnostics.Arrange("request", request);
        diagnostics.Arrange("inputs", inputs);

        AwsSigV4Inputs? defaultValue = request.AwsSigV4;
        AwsSigV4Inputs? keptValue = (request with { AwsSigV4 = inputs }).AwsSigV4;

        diagnostics.Act("default AwsSigV4", defaultValue);
        diagnostics.Act("kept AwsSigV4", keptValue);
        diagnostics.Assert("default AwsSigV4", null, defaultValue);
        Assert.IsNull(request.AwsSigV4);
        Assert.AreSame(inputs, (request with { AwsSigV4 = inputs }).AwsSigV4);
    }
}
