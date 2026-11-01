namespace Curl.Extended.Protocol.AzureStorage.Bodies;

/// <summary>
/// Pins the <c>Error</c> body reading of the Azure Storage REST API "Status and error codes", against
/// its error response body.
/// </summary>
[TestClass]
public sealed class AzureStorageErrorParserTests
{
    [TestMethod]
    public void Parse_ReferenceErrorResponse_ReadsCodeAndMessage()
    {
        // "Status and error codes", "Error response" body.
        const string Body = """
            <?xml version="1.0" encoding="utf-8"?>
            <Error>
              <Code>BlobNotFound</Code>
              <Message>The specified blob does not exist.</Message>
            </Error>
            """;

        var result = AzureStorageErrorParser.Parse(Body);

        Assert.AreEqual(new AzureStorageError("BlobNotFound", "The specified blob does not exist."), result.Value);
    }

    [TestMethod]
    public void Parse_ErrorWithoutMessage_ReadsAnEmptyMessage()
    {
        var result = AzureStorageErrorParser.Parse("<Error><Code>AuthenticationFailed</Code></Error>");

        Assert.AreEqual(new AzureStorageError("AuthenticationFailed", string.Empty), result.Value);
    }

    [TestMethod]
    [DataRow("<Error><Message>m</Message></Error>", DisplayName = "no Code")]
    [DataRow("", DisplayName = "empty body")]
    [DataRow("<html><body>Bad Gateway</body></html>", DisplayName = "another root")]
    public void Parse_MalformedErrorBody_YieldsAFailure(string body)
    {
        var result = AzureStorageErrorParser.Parse(body);

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(string.IsNullOrEmpty(result.Failure));
    }

    [TestMethod]
    public void Parse_NullBody_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => AzureStorageErrorParser.Parse(null!));
}
