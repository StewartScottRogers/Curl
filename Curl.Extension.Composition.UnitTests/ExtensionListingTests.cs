using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;

namespace Curl.Extension.Composition;

[TestClass]
public sealed class ExtensionListingTests
{
    private static readonly ExtensionDescription Cache = new(
        "cache",
        "Keeps responses on disk.",
        ["cache", "caches"],
        [new("cache-dir", ExtensionOptionArgument.Value, "Keep responses in <dir>"), new("cache-off", ExtensionOptionArgument.None, "Skip the cache")],
        new Uri("https://example.com/cache"));

    private static readonly ExtensionDescription Plain = new("plain", "Adds nothing.", [], [], new Uri("https://example.com/plain"));

    [TestMethod]
    public void SchemesOf_GivesEverySchemeInOrderEachOnce()
    {
        ExtensionDescription again = new("again", "Repeats a scheme.", ["CACHE", "again"], [], new Uri("https://example.com/again"));

        CollectionAssert.AreEqual(new[] { "cache", "caches", "again" }, ExtensionListing.SchemesOf([Cache, Plain, again]).ToArray());
    }

    [TestMethod]
    public void Schemes_WithExtensionsOn_AreTheCatalogues()
    {
        CollectionAssert.AreEqual(ExtensionListing.SchemesOf(ExtensionDescriptionCatalogue.Entries).ToArray(), ExtensionListing.Schemes(_ => null).ToArray());
        CollectionAssert.Contains(ExtensionListing.Schemes(_ => null).ToArray(), "coap");
    }

    [TestMethod]
    public void Schemes_WithCurlExtensionsZero_AreNone()
    {
        Assert.AreEqual(0, ExtensionListing.Schemes(name => name == "CURL_EXTENSIONS" ? "0" : null).Count);
    }

    [TestMethod]
    public void LinesOf_GivesOneLinePerExtension()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "cache: Keeps responses on disk. Schemes: cache, caches. Options: --cache-dir, --cache-off.",
                "plain: Adds nothing. Schemes: none. Options: none.",
            },
            ExtensionListing.LinesOf([Cache, Plain]).ToArray());
    }

    [TestMethod]
    public void MarkdownOf_GivesASectionPerExtension()
    {
        string markdown = ExtensionListing.MarkdownOf([Cache, Plain], off: false);

        StringAssert.StartsWith(markdown, "# extensions\n\n");
        StringAssert.Contains(markdown, "## cache\n\nKeeps responses on disk.\n\n- Schemes: `cache://`, `caches://`\n- Options: `--cache-dir <value>` (Keep responses in <dir>), `--cache-off` (Skip the cache)\n- Specification: <https://example.com/cache>\n\n");
        StringAssert.EndsWith(markdown, "## plain\n\nAdds nothing.\n\n- Schemes: none\n- Options: none\n- Specification: <https://example.com/plain>\n\n");
    }

    [TestMethod]
    public void MarkdownOf_WithExtensionsOff_SaysSoInsteadOfTheSections()
    {
        string markdown = ExtensionListing.MarkdownOf([Cache], off: true);

        StringAssert.EndsWith(markdown, "\n\nExtensions are off: `CURL_EXTENSIONS` is `0`.\n");
        Assert.IsFalse(markdown.Contains("## cache", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Lines_WithExtensionsOff_GivesTheOffLine()
    {
        CollectionAssert.AreEqual(new[] { ExtensionListing.OffLine }, ExtensionListing.Lines(name => name == "CURL_EXTENSIONS" ? "0" : null).ToArray());
    }

    [TestMethod]
    public void Lines_WithExtensionsOn_GivesTheRegisteredExtensions()
    {
        CollectionAssert.AreEqual(ExtensionListing.LinesOf(ExtensionDescriptionCatalogue.Entries).ToArray(), ExtensionListing.Lines(_ => null).ToArray());
    }

    [TestMethod]
    public void Markdown_FollowsTheSwitch()
    {
        Assert.AreEqual(ExtensionListing.MarkdownOf(ExtensionDescriptionCatalogue.Entries, off: true), ExtensionListing.Markdown(_ => "0"));
        Assert.AreEqual(ExtensionListing.MarkdownOf(ExtensionDescriptionCatalogue.Entries, off: false), ExtensionListing.Markdown(_ => null));
    }

    [TestMethod]
    public void Lines_WithoutAnEnvironmentReader_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ExtensionListing.Lines(null!));
    }
}
