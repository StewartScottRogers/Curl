namespace Curl.Extension.StructuredFields.UnitTests;

[TestClass]
public sealed class KnownStructuredFieldsTests
{
    [TestMethod]
    public void Types_NamesEachFieldByItsSpecificationsTopLevelTypeInAnyCase()
    {
        Assert.AreEqual(StructuredFieldType.List, KnownStructuredFields.Types["cache-status"]);
        Assert.AreEqual(StructuredFieldType.Dictionary, KnownStructuredFields.Types["Priority"]);
        Assert.AreEqual(StructuredFieldType.Item, KnownStructuredFields.Types["CROSS-ORIGIN-OPENER-POLICY"]);
        Assert.IsFalse(KnownStructuredFields.Types.ContainsKey("Cache-Control"));
    }

    [TestMethod]
    public void Describe_AValueOfEachType_GivesItsReadableLine()
    {
        Assert.AreEqual("List of 1: Token ExampleCache [hit=Boolean true, ttl=Integer 376]", KnownStructuredFields.Describe("ExampleCache; hit; ttl=376", StructuredFieldType.List));
        Assert.AreEqual("Dictionary of 2: u=Integer 3; i=Boolean true", KnownStructuredFields.Describe("u=3, i", StructuredFieldType.Dictionary));
        Assert.AreEqual("Token same-origin", KnownStructuredFields.Describe("same-origin", StructuredFieldType.Item));
    }

    [TestMethod]
    public void Describe_AValueThatDoesNotParse_SaysWhyAndThatItIsIgnored()
    {
        Assert.AreEqual("not a valid Dictionary (a comma ends the value), ignored", KnownStructuredFields.Describe("u=3, ", StructuredFieldType.Dictionary));
        Assert.AreEqual("not a valid Item (an item is missing), ignored", KnownStructuredFields.Describe(string.Empty, StructuredFieldType.Item));
    }
}
