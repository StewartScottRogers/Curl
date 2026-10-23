using Curl.Cli;
using Curl.Mcp.Protocol.Model;
using Curl.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

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

    [TestMethod]
    public async Task Provider_ListAndReadEqualTheSdkHandlersForEveryCatalogueResource()
    {
        using IHost host = CurlMcpHost.Build([], _ => null);
        McpServerOptions options = host.Services.GetRequiredService<IOptions<McpServerOptions>>().Value;
        await using McpServer server = McpServer.Create(new StreamServerTransport(Stream.Null, Stream.Null), options, null, host.Services);
        AiHelpMcpResourceProvider provider = new();

        ListResourcesResult sdkList = await options.Handlers.ListResourcesHandler!(
            new RequestContext<ListResourcesRequestParams>(server, new JsonRpcRequest { Method = RequestMethods.ResourcesList }, new ListResourcesRequestParams()),
            CancellationToken.None);
        IReadOnlyList<McpResource> list = provider.ListResources();

        Assert.AreEqual(sdkList.Resources.Count, list.Count);
        for (int index = 0; index < list.Count; index++)
        {
            Assert.AreEqual(sdkList.Resources[index].Uri, list[index].Uri);
            Assert.AreEqual(sdkList.Resources[index].Name, list[index].Name);
            Assert.AreEqual(sdkList.Resources[index].Description, list[index].Description);
            Assert.AreEqual(sdkList.Resources[index].MimeType, list[index].MimeType);

            ReadResourceResult sdkRead = await options.Handlers.ReadResourceHandler!(
                new RequestContext<ReadResourceRequestParams>(server, new JsonRpcRequest { Method = RequestMethods.ResourcesRead }, new ReadResourceRequestParams { Uri = list[index].Uri }),
                CancellationToken.None);
            TextResourceContents expected = (TextResourceContents)sdkRead.Contents.Single();
            McpResourceContents? actual = await provider.ReadAsync(list[index].Uri, CancellationToken.None);
            Assert.IsNotNull(actual);
            Assert.AreEqual(expected.Uri, actual.Uri);
            Assert.AreEqual(expected.MimeType, actual.MimeType);
            Assert.AreEqual(expected.Text, actual.Text);
        }
    }
}
