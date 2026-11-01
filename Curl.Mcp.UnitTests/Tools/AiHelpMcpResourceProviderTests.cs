using Curl.Cli;
using Curl.Mcp.Protocol.Model;
using Curl.Mcp.Tools;

namespace Curl.Mcp;

[TestClass]
[DoNotParallelize]
public sealed class AiHelpMcpResourceProviderTests
{
    private const string ExtensionsMarkdown = "# Curl's extensions\n";

    private static AiHelpMcpResourceProvider CreateProvider() => new(() => ExtensionsMarkdown);

    [TestMethod]
    public void ListResources_ListsEveryCatalogueResourceThenTheExtensionsWithMarkdownMimeType()
    {
        IReadOnlyList<McpResource> resources = CreateProvider().ListResources();

        CollectionAssert.AreEqual(
            McpAiHelpResourceCatalogue.Resources.Select(resource => resource.Uri).Append("curl://ai-help/extensions").ToArray(),
            resources.Select(resource => resource.Uri).ToArray());
        Assert.IsTrue(resources.All(resource => resource.MimeType == "text/markdown"));
        McpResource http = resources.Single(resource => resource.Uri == "curl://ai-help/http");
        Assert.AreEqual("ai-help-http", http.Name);
        Assert.AreEqual(McpAiHelpResourceCatalogue.Find("curl://ai-help/http")!.Description, http.Description);
        McpResource extensions = resources[^1];
        Assert.AreEqual("ai-help-extensions", extensions.Name);
        StringAssert.Contains(extensions.Description, "(curl --ai-help extensions)");
    }

    [TestMethod]
    public async Task ReadAsync_ACategoryResource_IsItsMarkdownAsOneTextContent()
    {
        McpResourceContents? contents = await CreateProvider().ReadAsync("curl://ai-help/http", CancellationToken.None);

        _ = CurlAiHelpText.TryGetMarkdown("http", out string expected);
        Assert.IsNotNull(contents);
        Assert.AreEqual(expected, contents.Text);
        Assert.AreEqual("curl://ai-help/http", contents.Uri);
        Assert.AreEqual("text/markdown", contents.MimeType);
    }

    [TestMethod]
    public async Task ReadAsync_TheExtensionsResource_IsWhatTheReaderGives()
    {
        McpResourceContents? contents = await CreateProvider().ReadAsync("curl://ai-help/extensions", CancellationToken.None);

        Assert.IsNotNull(contents);
        Assert.AreEqual(ExtensionsMarkdown, contents.Text);
        Assert.AreEqual("curl://ai-help/extensions", contents.Uri);
        Assert.AreEqual("text/markdown", contents.MimeType);
    }

    [TestMethod]
    [DataRow("curl://ai-help/nope")]
    [DataRow("")]
    public async Task ReadAsync_AnUnknownOrEmptyUri_IsNull(string uri)
    {
        McpResourceContents? contents = await CreateProvider().ReadAsync(uri, CancellationToken.None);

        Assert.IsNull(contents);
    }
}
