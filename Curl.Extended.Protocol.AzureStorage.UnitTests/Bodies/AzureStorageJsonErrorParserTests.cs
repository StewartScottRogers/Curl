namespace Curl.Extended.Protocol.AzureStorage.Bodies;

/// <summary>
/// Pins the JSON error body reading of the ADLS Gen2 (DFS) REST API, against its documented error
/// response body.
/// </summary>
[TestClass]
public sealed class AzureStorageJsonErrorParserTests
{
    [TestMethod]
    public void Parse_ReferenceErrorResponse_ReadsCodeAndMessage()
    {
        // ADLS Gen2 REST API error response body.
        const string Body = """{"error":{"code":"PathNotFound","message":"The specified path does not exist."}}""";

        var result = AzureStorageJsonErrorParser.Parse(Body);

        Assert.AreEqual(new AzureStorageError("PathNotFound", "The specified path does not exist."), result.Value);
    }

    [TestMethod]
    public void Parse_ErrorWithoutMessage_ReadsAnEmptyMessage()
    {
        var result = AzureStorageJsonErrorParser.Parse("""{"error":{"code":"AuthorizationFailure"}}""");

        Assert.AreEqual(new AzureStorageError("AuthorizationFailure", string.Empty), result.Value);
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty body")]
    [DataRow("[]", DisplayName = "not an object")]
    [DataRow("{}", DisplayName = "no error")]
    [DataRow("""{"error":"x"}""", DisplayName = "error not an object")]
    [DataRow("""{"error":{"message":"m"}}""", DisplayName = "no code")]
    [DataRow("""{"error":{"code":1}}""", DisplayName = "code not a string")]
    [DataRow("""{"error":{"code":"c","message":2}}""", DisplayName = "message not a string")]
    public void Parse_MalformedErrorBody_YieldsAFailure(string body)
    {
        var result = AzureStorageJsonErrorParser.Parse(body);

        Assert.IsNull(result.Value);
        Assert.IsNotNull(result.Failure);
    }

    [TestMethod]
    public void Parse_NullBody_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => AzureStorageJsonErrorParser.Parse(null!));
    }
}
