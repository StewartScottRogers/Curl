namespace Curl.Extended.Protocol.AzureStorage.Bodies;

/// <summary>
/// Pins the JSON body reading of the ADLS Gen2 REST API "Path - List", against its response body
/// (<c>PathList</c>, whose <c>Path</c> entries the service sends with string-valued fields).
/// </summary>
[TestClass]
public sealed class AzureStoragePathListParserTests
{
    [TestMethod]
    public void Parse_ReferenceResponse_ReadsEveryPath()
    {
        // "Path - List", "Responses", 200 OK: PathList { paths: Path[] }.
        const string Body = """
            {"paths":[
              {"contentLength":"0","etag":"0x8D7A2B3C4D5E6F7","group":"$superuser","isDirectory":"true","lastModified":"Thu, 16 Jan 2020 22:00:00 GMT","name":"logs","owner":"$superuser","permissions":"rwxr-x---"},
              {"contentLength":"1024","etag":"0x8D7A2B3C4D5E6F8","group":"$superuser","lastModified":"Thu, 16 Jan 2020 22:01:00 GMT","name":"logs/a.txt","owner":"$superuser","permissions":"rw-r-----"}
            ]}
            """;

        var paths = AzureStoragePathListParser.Parse(Body).Value!.Paths;

        Assert.AreEqual(new AzureStoragePath("logs", true, 0, new DateTimeOffset(2020, 1, 16, 22, 0, 0, TimeSpan.Zero), "0x8D7A2B3C4D5E6F7"), paths[0]);
        Assert.AreEqual(new AzureStoragePath("logs/a.txt", false, 1024, new DateTimeOffset(2020, 1, 16, 22, 1, 0, TimeSpan.Zero), "0x8D7A2B3C4D5E6F8"), paths[1]);
    }

    [TestMethod]
    public void Parse_JsonBooleanAndNumber_ReadAsTheStringsDo()
    {
        var paths = AzureStoragePathListParser.Parse("""{"paths":[{"name":"d","isDirectory":true},{"name":"f","isDirectory":false,"contentLength":7}]}""").Value!.Paths;

        Assert.AreEqual(new AzureStoragePath("d", true, 0, null, null), paths[0]);
        Assert.AreEqual(new AzureStoragePath("f", false, 7, null, null), paths[1]);
    }

    [TestMethod]
    public void Parse_EmptyPaths_ReadsAnEmptyPage()
    {
        var result = AzureStoragePathListParser.Parse("""{"paths":[]}""");

        Assert.IsTrue(result.Succeeded);
        Assert.IsEmpty(result.Value!.Paths);
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty body")]
    [DataRow("{\"paths\":[", DisplayName = "truncated JSON")]
    [DataRow("[]", DisplayName = "root not an object")]
    [DataRow("{}", DisplayName = "no paths")]
    [DataRow("{\"paths\":{}}", DisplayName = "paths not an array")]
    [DataRow("{\"paths\":[1]}", DisplayName = "entry not an object")]
    [DataRow("{\"paths\":[{}]}", DisplayName = "no name")]
    [DataRow("{\"paths\":[{\"name\":1}]}", DisplayName = "name not a string")]
    [DataRow("{\"paths\":[{\"name\":\"a\",\"isDirectory\":\"yes\"}]}", DisplayName = "isDirectory not a boolean string")]
    [DataRow("{\"paths\":[{\"name\":\"a\",\"isDirectory\":1}]}", DisplayName = "isDirectory a number")]
    [DataRow("{\"paths\":[{\"name\":\"a\",\"contentLength\":-1}]}", DisplayName = "negative contentLength")]
    [DataRow("{\"paths\":[{\"name\":\"a\",\"contentLength\":1.5}]}", DisplayName = "fractional contentLength")]
    [DataRow("{\"paths\":[{\"name\":\"a\",\"contentLength\":\"x\"}]}", DisplayName = "contentLength not a number string")]
    [DataRow("{\"paths\":[{\"name\":\"a\",\"contentLength\":\"99999999999999999999\"}]}", DisplayName = "contentLength past a long")]
    [DataRow("{\"paths\":[{\"name\":\"a\",\"contentLength\":true}]}", DisplayName = "contentLength a boolean")]
    [DataRow("{\"paths\":[{\"name\":\"a\",\"lastModified\":\"yesterday\"}]}", DisplayName = "lastModified not RFC 1123")]
    public void Parse_MalformedPathList_YieldsAFailure(string body)
    {
        var result = AzureStoragePathListParser.Parse(body);

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(string.IsNullOrEmpty(result.Failure));
    }

    [TestMethod]
    public void Parse_NullBody_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => AzureStoragePathListParser.Parse(null!));
}
