using System.Text.Json;
using Curl.Mcp.Tools;

namespace Curl.Mcp;

/// <summary>The <c>transfer</c> tool's <c>proxyUser</c>, <c>proxyCaFile</c>, <c>resolve</c> and <c>maxBodyBytes</c> fields (BL-5671).</summary>
[TestClass]
public sealed class McpTransferProxyAndLimitFieldsTests
{
    private const int ThreeMiB = 3 * 1024 * 1024;

    [TestMethod]
    public void Build_ProxyUserResolveAndProxyCa_AreSetOnTheOptions()
    {
        var request = new McpTransferRequest("http://h/") { ProxyUser = "me:secret", Resolve = ["h:80:127.0.0.1"] };

        var options = McpTransferOptions.Build(request, null, null, "/root/proxy-ca.pem");

        Assert.AreEqual("me:secret", options.Authentication.ProxyUserPassword);
        Assert.AreEqual("h:80:127.0.0.1", options.Network.Resolve.Single());
        Assert.AreEqual("/root/proxy-ca.pem", options.Tls.Proxy.CertificateAuthorityFile);
    }

    [TestMethod]
    public void Build_NoProxyFields_LeavesThemUnset()
    {
        var options = McpTransferOptions.Build(new McpTransferRequest("http://h/"), null);

        Assert.IsNull(options.Authentication.ProxyUserPassword);
        Assert.IsEmpty(options.Network.Resolve);
        Assert.IsNull(options.Tls.Proxy.CertificateAuthorityFile);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(McpResponseCap.CeilingMaxBytes + 1)]
    public void Check_MaxBodyBytesOutOfRange_NamesTheField(int value)
    {
        McpTransferRequestCheck check = McpTransferRequestCheck.Check(new McpTransferRequest("http://h/") { MaxBodyBytes = value });

        Assert.IsFalse(check.IsValid);
        StringAssert.Contains(check.Failure!.Text, "maxBodyBytes");
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(McpResponseCap.CeilingMaxBytes)]
    public void Check_MaxBodyBytesInRange_IsValid(int value) =>
        Assert.IsTrue(McpTransferRequestCheck.Check(new McpTransferRequest("http://h/") { MaxBodyBytes = value }).IsValid);

    [TestMethod]
    public void CheckArgumentShapes_WrongShapes_AreRefused()
    {
        JsonElement number = JsonDocument.Parse("1").RootElement;
        JsonElement fraction = JsonDocument.Parse("1.5").RootElement;
        JsonElement text = JsonDocument.Parse("\"x\"").RootElement;
        JsonElement mixed = JsonDocument.Parse("[\"a\",1]").RootElement;

        Assert.IsNotNull(McpTransferRequestCheck.CheckArgumentShapes([new("proxyUser", number)]));
        Assert.IsNotNull(McpTransferRequestCheck.CheckArgumentShapes([new("proxyCaFile", number)]));
        Assert.IsNotNull(McpTransferRequestCheck.CheckArgumentShapes([new("resolve", text)]));
        Assert.IsNotNull(McpTransferRequestCheck.CheckArgumentShapes([new("resolve", mixed)]));
        Assert.IsNotNull(McpTransferRequestCheck.CheckArgumentShapes([new("maxBodyBytes", text)]));
        Assert.IsNotNull(McpTransferRequestCheck.CheckArgumentShapes([new("maxBodyBytes", fraction)]));
    }

    [TestMethod]
    public void CheckArgumentShapes_NullsAndRightShapes_AreAccepted()
    {
        JsonElement nothing = JsonDocument.Parse("null").RootElement;
        JsonElement strings = JsonDocument.Parse("[\"a\"]").RootElement;
        JsonElement number = JsonDocument.Parse("5").RootElement;

        Assert.IsNull(McpTransferRequestCheck.CheckArgumentShapes(
        [
            new("resolve", nothing), new("resolve", strings), new("maxBodyBytes", nothing), new("maxBodyBytes", number),
        ]));
    }

    [TestMethod]
    public async Task RunAsync_ProxyCaFileInsideRoot_IsPassedResolved()
    {
        string root = NewDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "p.pem"), "pem");
            FakeEasyTransfer transfer = new();

            McpTransferToolResult result = await Tool(transfer, new McpConfinementRoot(root))
                .RunAsync(new McpTransferRequest("https://h/") { ProxyCaFile = "p.pem", Proxy = "https://proxy/" }, CancellationToken.None);

            Assert.IsFalse(result.IsError);
            Assert.AreEqual(new McpConfinementRoot(root).Check("p.pem").ResolvedPath, transfer.Options.Tls.Proxy.CertificateAuthorityFile);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task RunAsync_ProxyCaFileOutsideRoot_IsPathRefusedNamingTheField()
    {
        string root = NewDirectory();
        string other = NewDirectory();
        try
        {
            string ca = Path.Combine(other, "p.pem");
            File.WriteAllText(ca, "pem");
            FakeEasyTransfer transfer = new();

            McpTransferToolResult result = await Tool(transfer, new McpConfinementRoot(root))
                .RunAsync(new McpTransferRequest("https://h/") { ProxyCaFile = ca }, CancellationToken.None);

            Assert.AreEqual(McpToolFailureKind.PathRefused, result.Failure!.Kind);
            StringAssert.Contains(result.Failure.Text, "proxyCaFile");
            Assert.AreEqual(0, transfer.Performs);
        }
        finally
        {
            Directory.Delete(root, true);
            Directory.Delete(other, true);
        }
    }

    [TestMethod]
    public async Task RunAsync_BothCaFilesRefused_NamesTheFirst()
    {
        McpTransferToolResult result = await Tool(new FakeEasyTransfer(), null)
            .RunAsync(new McpTransferRequest("https://h/") { CaFile = "a.pem", ProxyCaFile = "b.pem" }, CancellationToken.None);

        StringAssert.Contains(result.Failure!.Text, "caFile");
    }

    [TestMethod]
    public async Task RunAsync_ThreeMiBBodyAtTheDefault_IsTruncated()
    {
        FakeEasyTransfer transfer = new() { BodyChunks = [new byte[ThreeMiB]] };

        McpTransferToolResult result = await Tool(transfer, null).RunAsync(new McpTransferRequest("http://h/"), CancellationToken.None);

        Assert.IsTrue(result.IsBodyTruncated);
        Assert.AreEqual(McpResponseCap.DefaultMaxBytes, result.Body!.ReturnedBytes);
    }

    [TestMethod]
    public async Task RunAsync_ThreeMiBBodyWithMaxBodyBytesRaised_IsReturnedWhole()
    {
        FakeEasyTransfer transfer = new() { BodyChunks = [new byte[ThreeMiB]] };

        McpTransferToolResult result = await Tool(transfer, null)
            .RunAsync(new McpTransferRequest("http://h/") { MaxBodyBytes = 4 * 1024 * 1024 }, CancellationToken.None);

        Assert.IsFalse(result.IsBodyTruncated);
        Assert.AreEqual(ThreeMiB, result.Body!.ReturnedBytes);
    }

    [TestMethod]
    public async Task CallAsync_NewFields_AreReadIntoTheRequest()
    {
        FakeEasyTransfer transfer = new();
        TransferMcpTool tool = new(new McpTransferTool(new FakeTransferEngine(transfer), new McpResponseCap(McpResponseCap.DefaultMaxBytes)));
        using JsonDocument arguments = JsonDocument.Parse(
            """{"url":"http://h/","proxyUser":"u:p","resolve":["h:80:127.0.0.1","g:80:::1"],"maxBodyBytes":10,"proxyCaFile":null}""");

        var result = await tool.CallAsync(arguments.RootElement, CancellationToken.None);

        Assert.IsFalse(result.IsError);
        Assert.AreEqual("u:p", transfer.Options.Authentication.ProxyUserPassword);
        Assert.HasCount(2, transfer.Options.Network.Resolve);
    }

    [TestMethod]
    public void Description_NamesTheDefaultLimitAndTheCeiling()
    {
        TransferMcpTool tool = new(new McpTransferTool(new FakeTransferEngine(new FakeEasyTransfer()), new McpResponseCap(1)));

        StringAssert.Contains(tool.Description, "1 MiB (1048576 bytes)");
        StringAssert.Contains(tool.Description, "64 MiB (67108864 bytes)");
    }

    private static McpTransferTool Tool(FakeEasyTransfer transfer, McpConfinementRoot? root) =>
        new(new FakeTransferEngine(transfer), new McpResponseCap(McpResponseCap.DefaultMaxBytes), root);

    private static string NewDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "curl-mcp-proxy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
