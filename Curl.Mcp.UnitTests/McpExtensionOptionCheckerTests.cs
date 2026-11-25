using Curl.Extension.Composition;

namespace Curl.Mcp;

/// <summary>Tests that <see cref="McpArgumentChecker" /> knows the extensions' options and confines those that name a path (BL-5564).</summary>
[TestClass]
public sealed class McpExtensionOptionCheckerTests
{
    private string workspace = string.Empty;

    private string root = string.Empty;

    private string outside = string.Empty;

    private McpArgumentChecker checker = null!;

    [TestInitialize]
    public void CreateFolders()
    {
        workspace = Path.Combine(Path.GetTempPath(), "curl-mcp-ext-" + Guid.NewGuid().ToString("N"));
        root = Path.Combine(workspace, "root");
        outside = Path.Combine(workspace, "outside");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        checker = new McpArgumentChecker(new McpConfinementRoot(root), CurlMcpHost.ExtensionOptionsFor(_ => null));
    }

    [TestCleanup]
    public void DeleteFolders() => Directory.Delete(workspace, recursive: true);

    [TestMethod]
    public void Check_DnsJsonFlag_IsAllowed() =>
        Assert.IsTrue(checker.Check(["-s", "--dns-json", "dns://127.0.0.1:53/www.example.com"]).IsAllowed);

    [TestMethod]
    public void Check_NoDnsJsonNegation_IsAllowed() =>
        Assert.IsTrue(checker.Check(["--no-dns-json", "dns://127.0.0.1:53/www.example.com"]).IsAllowed);

    [TestMethod]
    public void Check_DnsTransportWithSeparateValue_IsAllowed() =>
        Assert.IsTrue(checker.Check(["--dns-transport", "tcp", "dns://127.0.0.1:53/www.example.com"]).IsAllowed);

    [TestMethod]
    public void Check_DnsTransportWithAttachedValue_IsAllowed() =>
        Assert.IsTrue(checker.Check(["--dns-transport=tcp"]).IsAllowed);

    [TestMethod]
    public void Check_ValueOptionWithoutValue_IsAllowedForCurlToReject() =>
        Assert.IsTrue(checker.Check(["--dns-transport"]).IsAllowed);

    [TestMethod]
    public void Check_UnregisteredExtensionOption_IsRefusedAsUnknown()
    {
        McpArgumentCheck check = new McpArgumentChecker(new McpConfinementRoot(root)).Check(["--dns-json"]);

        Assert.IsFalse(check.IsAllowed);
        StringAssert.Contains(check.Reason, "not an option this server knows");
    }

    [TestMethod]
    [DataRow("--record")]
    [DataRow("--replay")]
    [DataRow("--cache-dir")]
    [DataRow("--har")]
    [DataRow("--grpc-proto")]
    [DataRow("--wireguard")]
    [DataRow("--csharp")]
    public void Check_PathOptionOutsideTheRoot_IsRefused(string option)
    {
        McpArgumentCheck check = checker.Check([option, Path.Combine(outside, "x"), "http://h/"]);

        Assert.IsFalse(check.IsAllowed);
        Assert.AreEqual(option, check.Option);
    }

    [TestMethod]
    public void Check_PathOptionInsideTheRoot_IsAllowed() =>
        Assert.IsTrue(checker.Check(["--har", Path.Combine(root, "out.har"), "http://h/"]).IsAllowed);

    [TestMethod]
    public void Check_GraphQlVariablesFileOutsideTheRoot_IsRefused() =>
        Assert.IsFalse(checker.Check(["--graphql-variables", "@" + Path.Combine(outside, "v.json")]).IsAllowed);

    [TestMethod]
    public void Check_GraphQlVariablesJson_IsAllowed() =>
        Assert.IsTrue(checker.Check(["--graphql-variables", "{\"a\":1}"]).IsAllowed);

    [TestMethod]
    public void Check_OhttpKeysUrl_IsAllowedAndFileOutsideTheRootIsRefused()
    {
        Assert.IsTrue(checker.Check(["--ohttp-keys", "https://h/keys"]).IsAllowed);
        Assert.IsTrue(checker.Check(["--ohttp-keys", "HTTP://h/keys"]).IsAllowed);
        Assert.IsFalse(checker.Check(["--ohttp-keys", Path.Combine(outside, "k")]).IsAllowed);
    }

    [TestMethod]
    public void Check_TokenCacheEmpty_IsAllowedAndFolderOutsideTheRootIsRefused()
    {
        Assert.IsTrue(checker.Check(["--oauth2-token-cache", string.Empty]).IsAllowed);
        Assert.IsFalse(checker.Check(["--oauth2-token-cache", outside]).IsAllowed);
    }

    [TestMethod]
    public void Check_UnclassifiedValueOption_IsConfinedAsAPath()
    {
        McpArgumentChecker custom = new(new McpConfinementRoot(root), [new McpExtensionOption("brand-new", true)]);

        Assert.IsFalse(custom.Check(["--brand-new", Path.Combine(outside, "x")]).IsAllowed);
    }

    [TestMethod]
    public void RegisteredExtensionOptions_AreAllClassifiedByTheTable()
    {
        IReadOnlyList<McpExtensionOption> registered = CurlMcpHost.ExtensionOptionsFor(_ => null);

        Assert.IsTrue(registered.Count > 0);
        string[] unclassified = [.. registered.Where(option => !McpExtensionOptionPathTable.IsClassified(option.Name)).Select(option => option.Name)];
        Assert.AreEqual(0, unclassified.Length, "Classify in McpExtensionOptionPathTable: " + string.Join(", ", unclassified));
    }

    [TestMethod]
    public void RegisteredExtensionOptions_AreAllKnownToTheChecker()
    {
        foreach (McpExtensionOption option in CurlMcpHost.ExtensionOptionsFor(_ => null))
        {
            McpArgumentCheck check = checker.Check(["--" + option.Name]);
            Assert.IsTrue(check.IsAllowed, option.Name);
        }
    }

    [TestMethod]
    public void ExtensionOptionsFor_WithCurlExtensionsZero_IsEmpty() =>
        Assert.AreEqual(0, CurlMcpHost.ExtensionOptionsFor(name => name == ExtensionSwitch.Variable ? "0" : null).Count);

    [TestMethod]
    public void Table_PathAndNoPathNamesDoNotOverlap() =>
        Assert.AreEqual(0, McpExtensionOptionPathTable.PathOptionNames.Intersect(McpExtensionOptionPathTable.NoPathOptionNames).Count());
}
