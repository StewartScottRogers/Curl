namespace Curl.Extension.Protocol.GoogleCloudStorage.Xml;

/// <summary>
/// Pins the <c>Error</c> body reading of the Cloud Storage "XML API error responses", against its
/// example body.
/// </summary>
[TestClass]
public sealed class GcsErrorResponseParserTests
{
    [TestMethod]
    public void Parse_SpecificationExampleErrorResponse_ReadsCodeMessageAndDetails()
    {
        // "XML API error responses", the example error response body.
        const string Body = """
            <?xml version='1.0' encoding='UTF-8'?>
            <Error>
              <Code>NoSuchKey</Code>
              <Message>The specified key does not exist.</Message>
              <Details>No such object: travel-maps/paris.jpg</Details>
            </Error>
            """;

        var result = GcsErrorResponseParser.Parse(Body);

        Assert.AreEqual(
            new GcsErrorResponse("NoSuchKey", "The specified key does not exist.", "No such object: travel-maps/paris.jpg"),
            result.Value);
    }

    [TestMethod]
    public void Parse_ErrorWithOnlyCode_ReadsAnEmptyMessageAndNoDetails()
    {
        var result = GcsErrorResponseParser.Parse("<Error><Code>AccessDenied</Code></Error>");

        Assert.AreEqual(new GcsErrorResponse("AccessDenied", string.Empty, null), result.Value);
    }

    [TestMethod]
    [DataRow("<Error><Message>m</Message></Error>", DisplayName = "no Code")]
    [DataRow("", DisplayName = "empty body")]
    [DataRow("<html><body>Bad Gateway</body></html>", DisplayName = "another root")]
    public void Parse_MalformedErrorBody_YieldsAFailure(string body)
    {
        var result = GcsErrorResponseParser.Parse(body);

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(string.IsNullOrEmpty(result.Failure));
    }
}
