namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins that an <see cref="AwsSigV4Inputs" /> carries what <c>--aws-sigv4</c> signs, with no body,
/// no upload and no <c>--path-as-is</c> by default, and that an <see cref="HttpAuthRequest" />
/// carries none unless given (BL-629).
/// </summary>
[TestClass]
public sealed class AwsSigV4InputsTests
{
    [TestMethod]
    public void Constructor_RoundTripsTheParameterHostAndHeadersWithDefaultsForTheRest()
    {
        string[] headers = ["X-A: 1"];

        AwsSigV4Inputs inputs = new("aws:amz:us-east-1:s3", "127.0.0.1:18629", headers);

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
        byte[] body = [0x61];

        AwsSigV4Inputs inputs = new AwsSigV4Inputs("osc", "h", []) with
        {
            PostFields = body,
            UploadSize = 12,
            IsGetOrHead = true,
            PathAsIs = true,
        };

        CollectionAssert.AreEqual(body, inputs.PostFields!.Value.ToArray());
        Assert.AreEqual(12, inputs.UploadSize);
        Assert.IsTrue(inputs.IsGetOrHead);
        Assert.IsTrue(inputs.PathAsIs);
    }

    [TestMethod]
    public void HttpAuthRequestAwsSigV4_NullByDefaultAndKeptWhenSet()
    {
        HttpAuthRequest request = new("GET", CurlUrl.Parse("http://example.com/"), "/", null, null, HttpAuthSchemes.Basic, IsProxy: false);
        AwsSigV4Inputs inputs = new("aws", "example.com", []);

        Assert.IsNull(request.AwsSigV4);
        Assert.AreSame(inputs, (request with { AwsSigV4 = inputs }).AwsSigV4);
    }
}
