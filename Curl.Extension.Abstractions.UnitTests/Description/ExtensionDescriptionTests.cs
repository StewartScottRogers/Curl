namespace Curl.Extension.Abstractions.Description;

[TestClass]
public sealed class ExtensionDescriptionTests
{
    [TestMethod]
    public void Constructor_KeepsEveryPart()
    {
        ExtensionOption option = new("cache-dir", ExtensionOptionArgument.Value, "Cache folder");
        Uri specification = new("https://example.com/spec");

        ExtensionDescription description = new("cache", "Caches responses.", ["cache"], [option], specification);

        Assert.AreEqual("cache", description.Name);
        Assert.AreEqual("Caches responses.", description.Summary);
        CollectionAssert.AreEqual(new[] { "cache" }, description.Schemes.ToArray());
        CollectionAssert.AreEqual(new[] { option }, description.Options.ToArray());
        Assert.AreSame(specification, description.Specification);
    }
}
