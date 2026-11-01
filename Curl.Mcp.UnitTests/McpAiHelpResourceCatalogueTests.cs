using Curl.Cli;

namespace Curl.Mcp;

[TestClass]
public sealed class McpAiHelpResourceCatalogueTests
{
    [TestMethod]
    public void Resources_AreTheIndexThenAllThenOnePerCategoryInCategoryOrder()
    {
        string[] expected =
        [
            "curl://ai-help",
            "curl://ai-help/all",
            .. CurlHelpText.Categories.Select(category => "curl://ai-help/" + category.Name),
        ];

        CollectionAssert.AreEqual(expected, McpAiHelpResourceCatalogue.Resources.Select(resource => resource.Uri).ToArray());
    }

    [TestMethod]
    public void Resources_EachReadsWhatAiHelpPrintsForItsSubject()
    {
        foreach (McpAiHelpResource resource in McpAiHelpResourceCatalogue.Resources)
        {
            Assert.IsTrue(CurlAiHelpText.TryGetMarkdown(resource.Subject, out string expected), resource.Uri);
            Assert.AreEqual(expected, resource.ReadMarkdown(), resource.Uri);
        }
    }

    [TestMethod]
    public void Resources_IndexReadsTheNoSubjectIndexAndAllReadsTheFullReference()
    {
        _ = CurlAiHelpText.TryGetMarkdown(null, out string index);
        _ = CurlAiHelpText.TryGetMarkdown("all", out string all);

        Assert.AreEqual(index, McpAiHelpResourceCatalogue.Find("curl://ai-help")!.ReadMarkdown());
        Assert.AreEqual(all, McpAiHelpResourceCatalogue.Find("curl://ai-help/all")!.ReadMarkdown());
    }

    [TestMethod]
    public void Resources_HaveUniqueNamesAndADescriptionNamingTheirCommand()
    {
        Assert.AreEqual(McpAiHelpResourceCatalogue.Resources.Count, McpAiHelpResourceCatalogue.Resources.Select(resource => resource.Name).Distinct().Count());
        foreach (McpAiHelpResource resource in McpAiHelpResourceCatalogue.Resources)
        {
            StringAssert.Contains(resource.Description, ("curl --ai-help " + resource.Subject).TrimEnd() + ")", resource.Uri);
        }
    }

    [TestMethod]
    public void ResourcesWithExtensions_AreTheResourcesThenTheExtensionsReadingTheGivenMarkdown()
    {
        IReadOnlyList<McpAiHelpResource> resources = McpAiHelpResourceCatalogue.ResourcesWithExtensions(() => "# Extensions\n");

        CollectionAssert.AreEqual(
            McpAiHelpResourceCatalogue.Resources.Select(resource => resource.Uri).Append("curl://ai-help/extensions").ToArray(),
            resources.Select(resource => resource.Uri).ToArray());
        McpAiHelpResource extensions = McpAiHelpResourceCatalogue.Find(resources, "curl://ai-help/extensions")!;
        Assert.AreEqual("extensions", extensions.Subject);
        Assert.AreEqual("ai-help-extensions", extensions.Name);
        StringAssert.Contains(extensions.Description, "(curl --ai-help extensions)");
        Assert.AreEqual("# Extensions\n", extensions.ReadMarkdown());
        Assert.IsNull(McpAiHelpResourceCatalogue.Find("curl://ai-help/extensions"));
    }

    [TestMethod]
    public void Find_ACategoryUri_GivesThatCategory()
    {
        Assert.AreEqual("http", McpAiHelpResourceCatalogue.Find("curl://ai-help/http")!.Subject);
    }

    [TestMethod]
    [DataRow("curl://ai-help/nope")]
    [DataRow("curl://ai-help/HTTP")]
    [DataRow("curl://ai-help/")]
    [DataRow("")]
    public void Find_AnUnknownUri_GivesNull(string uri)
    {
        Assert.IsNull(McpAiHelpResourceCatalogue.Find(uri));
    }
}
