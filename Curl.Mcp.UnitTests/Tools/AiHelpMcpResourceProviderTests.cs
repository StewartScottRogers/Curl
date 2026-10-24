using Curl.Cli;
using Curl.Mcp.Protocol.Model;
using Curl.Mcp.Tools;

namespace Curl.Mcp;

[TestClass]
[DoNotParallelize]
public sealed class AiHelpMcpResourceProviderTests
{
    [TestMethod]
    public void ListResources_ListsEveryCatalogueResourceWithMarkdownMimeType()
    {
        IReadOnlyList<McpResource> resources = new AiHelpMcpResourceProvider().ListResources();

        CollectionAssert.AreEqual(
            McpAiHelpResourceCatalogue.Resources.Select(resource => resource.Uri).ToArray(),
            resources.Select(resource => resource.Uri).ToArray());
        Assert.IsTrue(resources.All(resource => resource.MimeType == "text/markdown"));
        McpResource http = resources.Single(resource => resource.Uri == "curl://ai-help/http");
        Assert.AreEqual("ai-help-http", http.Name);
        Assert.AreEqual(McpAiHelpResourceCatalogue.Find("curl://ai-help/http")!.Description, http.Description);
    }

    [TestMethod]
    public async Task ReadAsync_ACategoryResource_IsItsMarkdownAsOneTextContent()
    {
        McpResourceContents? contents = await new AiHelpMcpResourceProvider().ReadAsync("curl://ai-help/http", CancellationToken.None);

        _ = CurlAiHelpText.TryGetMarkdown("http", out string expected);
        Assert.IsNotNull(contents);
        Assert.AreEqual(expected, contents.Text);
        Assert.AreEqual("curl://ai-help/http", contents.Uri);
        Assert.AreEqual("text/markdown", contents.MimeType);
    }

    [TestMethod]
    [DataRow("curl://ai-help/nope")]
    [DataRow("")]
    public async Task ReadAsync_AnUnknownOrEmptyUri_IsNull(string uri)
    {
        McpResourceContents? contents = await new AiHelpMcpResourceProvider().ReadAsync(uri, CancellationToken.None);

        Assert.IsNull(contents);
    }
}
