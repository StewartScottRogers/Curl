using Curl.Api.Codes;
using Curl.Api.Options.Tls;

namespace Curl.Mcp;

/// <summary>The <c>transfer</c> tool's <c>minTlsVersion</c> and <c>caFile</c> fields (BL-5596).</summary>
[TestClass]
public sealed class McpTransferTlsFieldsTests
{
    private static readonly McpResponseCap Cap = new(16);

    [TestMethod]
    [DataRow("1.0", SslVersion.Tls10)]
    [DataRow("1.1", SslVersion.Tls11)]
    [DataRow("1.2", SslVersion.Tls12)]
    [DataRow("1.3", SslVersion.Tls13)]
    [DataRow(null, SslVersion.Default)]
    public void Build_MinTlsVersion_SetsTheLowestVersion(string? version, SslVersion expected)
    {
        var options = McpTransferOptions.Build(new McpTransferRequest("https://h/") { MinTlsVersion = version }, null);

        Assert.AreEqual(expected, options.Tls.Origin.SslVersion);
    }

    [TestMethod]
    public void Build_CaFilePathWithInsecure_SetsBoth()
    {
        var options = McpTransferOptions.Build(new McpTransferRequest("https://h/") { Insecure = true }, null, "/root/ca.pem");

        Assert.AreEqual("/root/ca.pem", options.Tls.Origin.CertificateAuthorityFile);
        Assert.IsFalse(options.Tls.Origin.VerifyPeer);
    }

    [TestMethod]
    public void Check_UnknownMinTlsVersion_NamesTheField()
    {
        McpTransferRequestCheck check = McpTransferRequestCheck.Check(new McpTransferRequest("https://h/") { MinTlsVersion = "1.4" });

        Assert.IsFalse(check.IsValid);
        StringAssert.Contains(check.Failure!.Text, "minTlsVersion");
    }

    [TestMethod]
    public void CheckArgumentShapes_NonStringTlsFields_AreRefused()
    {
        System.Text.Json.JsonElement number = System.Text.Json.JsonDocument.Parse("1").RootElement;

        Assert.IsNotNull(McpTransferRequestCheck.CheckArgumentShapes([new("minTlsVersion", number)]));
        Assert.IsNotNull(McpTransferRequestCheck.CheckArgumentShapes([new("caFile", number)]));
    }

    [TestMethod]
    public async Task RunAsync_CaFileInsideRoot_IsPassedResolvedAndTheTransferSucceeds()
    {
        string root = NewDirectory();
        try
        {
            string ca = Path.Combine(root, "ca.pem");
            File.WriteAllText(ca, "pem");
            FakeEasyTransfer transfer = new();

            McpTransferToolResult result = await Tool(transfer, new McpConfinementRoot(root))
                .RunAsync(new McpTransferRequest("https://h/") { CaFile = "ca.pem", MinTlsVersion = "1.2" }, CancellationToken.None);

            Assert.IsFalse(result.IsError);
            Assert.AreEqual(new McpConfinementRoot(root).Check("ca.pem").ResolvedPath, transfer.Options.Tls.Origin.CertificateAuthorityFile);
            Assert.AreEqual(SslVersion.Tls12, transfer.Options.Tls.Origin.SslVersion);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task RunAsync_NoCaFileAndAVerificationFailure_IsExit60()
    {
        FakeEasyTransfer transfer = new() { Code = CurlCode.PeerFailedVerification, ErrorTextToReport = "SSL certificate problem" };

        McpTransferToolResult result = await Tool(transfer, null).RunAsync(new McpTransferRequest("https://h/"), CancellationToken.None);

        Assert.AreEqual(60, result.Failure!.ExitCode);
        Assert.IsNull(transfer.Options.Tls.Origin.CertificateAuthorityFile);
    }

    [TestMethod]
    public async Task RunAsync_CaFileOutsideRoot_IsPathRefusedAndNoTransferRuns()
    {
        string root = NewDirectory();
        string other = NewDirectory();
        try
        {
            string ca = Path.Combine(other, "ca.pem");
            File.WriteAllText(ca, "pem");
            FakeEasyTransfer transfer = new();

            McpTransferToolResult result = await Tool(transfer, new McpConfinementRoot(root))
                .RunAsync(new McpTransferRequest("https://h/") { CaFile = ca }, CancellationToken.None);

            Assert.AreEqual(McpToolFailureKind.PathRefused, result.Failure!.Kind);
            Assert.AreEqual(0, transfer.Performs);
        }
        finally
        {
            Directory.Delete(root, true);
            Directory.Delete(other, true);
        }
    }

    [TestMethod]
    public async Task RunAsync_CaFileWithNoRootConfigured_IsPathRefused()
    {
        McpTransferToolResult result = await Tool(new FakeEasyTransfer(), null)
            .RunAsync(new McpTransferRequest("https://h/") { CaFile = "ca.pem" }, CancellationToken.None);

        Assert.AreEqual(McpToolFailureKind.PathRefused, result.Failure!.Kind);
    }

    private static McpTransferTool Tool(FakeEasyTransfer transfer, McpConfinementRoot? root) =>
        new(new FakeTransferEngine(transfer), Cap, root);

    private static string NewDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "curl-mcp-tls-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
