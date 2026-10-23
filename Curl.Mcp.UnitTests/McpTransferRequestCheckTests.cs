using System.Text;

namespace Curl.Mcp;

[TestClass]
public sealed class McpTransferRequestCheckTests
{
    [TestMethod]
    [DataRow("", "url")]
    [DataRow("   ", "url")]
    [DataRow("example.test/path", "url")]
    [DataRow("http://exa mple.test/", "url")]
    [DataRow("file:///etc/passwd", "url")]
    public void Check_BadUrl_NamesTheUrlField(string url, string field)
    {
        McpTransferRequestCheck check = McpTransferRequestCheck.Check(new McpTransferRequest(url));

        Assert.IsFalse(check.IsValid);
        Assert.AreEqual(McpToolFailureKind.InvalidRequest, check.Failure!.Kind);
        StringAssert.StartsWith(check.Failure.Text, $"Invalid {field}:");
        Assert.IsNull(check.Body);
    }

    [TestMethod]
    public void Check_FileUrl_PointsAtTheCurlTool()
    {
        McpTransferRequestCheck check = McpTransferRequestCheck.Check(new McpTransferRequest("file:///dir/x"));

        StringAssert.Contains(check.Failure!.Text, "use the curl tool");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("GE T")]
    [DataRow("GET\r\n")]
    [DataRow("PÜT")]
    public void Check_MethodThatIsNotAToken_NamesTheMethodField(string method)
    {
        McpTransferRequestCheck check = McpTransferRequestCheck.Check(new McpTransferRequest("http://h/") { Method = method });

        StringAssert.StartsWith(check.Failure!.Text, "Invalid method:");
    }

    [TestMethod]
    [DataRow("PROPFIND")]
    [DataRow("get")]
    [DataRow("X-Custom_Method.1~")]
    public void Check_TokenMethod_IsValid(string method)
    {
        Assert.IsTrue(McpTransferRequestCheck.Check(new McpTransferRequest("http://h/") { Method = method }).IsValid);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("Bad:Name")]
    [DataRow("Bad Name")]
    public void Check_HeaderWithoutAName_NamesItsIndex(string name)
    {
        McpTransferRequest request = new("http://h/") { Headers = [new McpTransferHeader("Good", "1"), new McpTransferHeader(name, "v")] };

        StringAssert.StartsWith(McpTransferRequestCheck.Check(request).Failure!.Text, "Invalid headers[1].name:");
    }

    [TestMethod]
    [DataRow("a\r\nX-Injected: 1")]
    [DataRow("a\nb")]
    public void Check_HeaderValueWithALineBreak_NamesItsValue(string value)
    {
        McpTransferRequest request = new("http://h/") { Headers = [new McpTransferHeader("X-A", value)] };

        StringAssert.StartsWith(McpTransferRequestCheck.Check(request).Failure!.Text, "Invalid headers[0].value:");
    }

    [TestMethod]
    [DataRow(-1d)]
    [DataRow(-0.001d)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(1e12d)]
    public void Check_TimeoutOutOfRange_NamesTheTimeoutField(double seconds)
    {
        McpTransferRequestCheck check = McpTransferRequestCheck.Check(new McpTransferRequest("http://h/") { TimeoutSeconds = seconds });

        StringAssert.StartsWith(check.Failure!.Text, "Invalid timeoutSeconds:");
    }

    [TestMethod]
    [DataRow(0d)]
    [DataRow(30d)]
    [DataRow(McpTransferRequestCheck.MaxTimeoutSeconds)]
    public void Check_TimeoutInRange_IsValid(double seconds)
    {
        Assert.IsTrue(McpTransferRequestCheck.Check(new McpTransferRequest("http://h/") { TimeoutSeconds = seconds }).IsValid);
    }

    [TestMethod]
    public void Check_HeadWithABody_NamesTheBodyField()
    {
        McpTransferRequest request = new("http://h/") { Method = "HEAD", Body = new McpTransferBody("x", false) };

        StringAssert.StartsWith(McpTransferRequestCheck.Check(request).Failure!.Text, "Invalid body:");
    }

    [TestMethod]
    public void Check_BadBase64Body_NamesTheBodyField()
    {
        McpTransferRequest request = new("http://h/") { Method = "POST", Body = new McpTransferBody("not base64!", true) };

        McpTransferRequestCheck check = McpTransferRequestCheck.Check(request);

        StringAssert.StartsWith(check.Failure!.Text, "Invalid body:");
        Assert.IsNull(check.Body);
    }

    [TestMethod]
    public void Check_TextBody_IsItsUtf8Bytes()
    {
        McpTransferRequestCheck check = McpTransferRequestCheck.Check(new McpTransferRequest("http://h/") { Body = new McpTransferBody("é", false) });

        Assert.IsTrue(check.IsValid);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("é"), check.Body);
    }

    [TestMethod]
    public void Check_Base64Body_IsTheDecodedBytes()
    {
        McpTransferRequestCheck check = McpTransferRequestCheck.Check(new McpTransferRequest("http://h/") { Body = new McpTransferBody("/wD+", true) });

        CollectionAssert.AreEqual(new byte[] { 0xFF, 0x00, 0xFE }, check.Body);
    }

    [TestMethod]
    public void Check_NoBody_IsValidWithNoBytes()
    {
        McpTransferRequestCheck check = McpTransferRequestCheck.Check(new McpTransferRequest("sftp://h/file") { Method = "HEAD" });

        Assert.IsTrue(check.IsValid);
        Assert.IsNull(check.Body);
    }

    [TestMethod]
    public void Check_NullRequest_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => McpTransferRequestCheck.Check(null!));
    }

    [TestMethod]
    public void CheckArgumentShapes_NoArguments_IsNull()
    {
        Assert.IsNull(McpTransferRequestCheck.CheckArgumentShapes(null));
    }

    [TestMethod]
    public void CheckArgumentShapes_AnArgumentTheToolHasNoFieldFor_IsIgnored()
    {
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse("""{"unknown":5,"url":"https://example.test/"}""");

        McpToolFailure? failure = McpTransferRequestCheck.CheckArgumentShapes(document.RootElement.EnumerateObject().Select(property => KeyValuePair.Create(property.Name, property.Value)));

        Assert.IsNull(failure);
    }
}
