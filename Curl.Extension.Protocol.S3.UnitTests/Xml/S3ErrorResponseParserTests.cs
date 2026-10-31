namespace Curl.Extension.Protocol.S3.Xml;

/// <summary>
/// Pins the <c>Error</c> body reading of the Amazon S3 API reference "Error responses", against its
/// example body.
/// </summary>
[TestClass]
public sealed class S3ErrorResponseParserTests
{
    [TestMethod]
    public void Parse_ReferenceExampleErrorResponse_ReadsCodeAndMessage()
    {
        // "Error responses", "REST error responses", sample error response.
        const string Body = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Error>
              <Code>NoSuchKey</Code>
              <Message>The resource you requested does not exist</Message>
              <Resource>/mybucket/myfoto.jpg</Resource>
              <RequestId>4442587FB7D0A2F9</RequestId>
            </Error>
            """;

        var result = S3ErrorResponseParser.Parse(Body);

        Assert.AreEqual(new S3ErrorResponse("NoSuchKey", "The resource you requested does not exist"), result.Value);
    }

    [TestMethod]
    public void Parse_ErrorWithoutMessage_ReadsAnEmptyMessage()
    {
        var result = S3ErrorResponseParser.Parse("<Error><Code>AccessDenied</Code></Error>");

        Assert.AreEqual(new S3ErrorResponse("AccessDenied", string.Empty), result.Value);
    }

    [TestMethod]
    [DataRow("<Error><Message>m</Message></Error>", DisplayName = "no Code")]
    [DataRow("", DisplayName = "empty body")]
    [DataRow("<html><body>Bad Gateway</body></html>", DisplayName = "another root")]
    public void Parse_MalformedErrorBody_YieldsAFailure(string body)
    {
        var result = S3ErrorResponseParser.Parse(body);

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(string.IsNullOrEmpty(result.Failure));
    }
}
