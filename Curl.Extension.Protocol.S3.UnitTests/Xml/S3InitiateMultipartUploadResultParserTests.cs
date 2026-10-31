namespace Curl.Extension.Protocol.S3.Xml;

/// <summary>
/// Pins the <c>InitiateMultipartUploadResult</c> reading of the Amazon S3 API reference
/// "CreateMultipartUpload", against its example body.
/// </summary>
[TestClass]
public sealed class S3InitiateMultipartUploadResultParserTests
{
    [TestMethod]
    public void Parse_ReferenceExampleInitiateMultipartUpload_ReadsTheUploadId()
    {
        // "CreateMultipartUpload", "Example 1: Initiate a multipart upload", sample response.
        const string Body = """
            <?xml version="1.0" encoding="UTF-8"?>
            <InitiateMultipartUploadResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/">
              <Bucket>example-bucket</Bucket>
              <Key>example-object</Key>
              <UploadId>VXBsb2FkIElEIGZvciA2aWWpbmcncyBteS1tb3ZpZS5tMnRzIHVwbG9hZA</UploadId>
            </InitiateMultipartUploadResult>
            """;

        var result = S3InitiateMultipartUploadResultParser.Parse(Body);

        Assert.AreEqual(
            new S3MultipartUpload("example-bucket", "example-object", "VXBsb2FkIElEIGZvciA2aWWpbmcncyBteS1tb3ZpZS5tMnRzIHVwbG9hZA"),
            result.Value);
    }

    [TestMethod]
    [DataRow("<InitiateMultipartUploadResult><Bucket>b</Bucket><Key>k</Key></InitiateMultipartUploadResult>", DisplayName = "no UploadId")]
    [DataRow("<InitiateMultipartUploadResult>", DisplayName = "not well-formed")]
    [DataRow("<ListBucketResult/>", DisplayName = "another root")]
    public void Parse_MalformedInitiateMultipartUploadResult_YieldsAFailure(string body)
    {
        var result = S3InitiateMultipartUploadResultParser.Parse(body);

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(string.IsNullOrEmpty(result.Failure));
    }
}
