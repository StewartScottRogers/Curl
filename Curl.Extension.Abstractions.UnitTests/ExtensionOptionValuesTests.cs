namespace Curl.Extension.Abstractions;

[TestClass]
public sealed class ExtensionOptionValuesTests
{
    private static readonly ExtensionOption CacheDirectory = new("cache-dir", ExtensionOptionArgument.Value, "Keep responses in <dir>");
    private static readonly ExtensionOption Sse = new("sse", ExtensionOptionArgument.None, "Read the response as server-sent events");

    [TestMethod]
    public void ValueOf_AGivenValueOption_GivesItsValue()
    {
        ExtensionOptionValues values = new(new Dictionary<string, string> { ["cache-dir"] = "/tmp/cache" });

        Assert.IsTrue(values.IsGiven(CacheDirectory));
        Assert.AreEqual("/tmp/cache", values.ValueOf(CacheDirectory));
    }

    [TestMethod]
    public void ValueOf_AGivenFlag_GivesTheEmptyString()
    {
        ExtensionOptionValues values = new(new Dictionary<string, string> { ["sse"] = string.Empty });

        Assert.IsTrue(values.IsGiven(Sse));
        Assert.AreEqual(string.Empty, values.ValueOf(Sse));
    }

    [TestMethod]
    public void None_GivesNoOption()
    {
        Assert.IsFalse(ExtensionOptionValues.None.IsGiven(Sse));
        Assert.IsNull(ExtensionOptionValues.None.ValueOf(CacheDirectory));
    }

    [TestMethod]
    public void IsGiven_WithNoOption_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ExtensionOptionValues.None.IsGiven(null!));

    [TestMethod]
    public void ValueOf_WithNoOption_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ExtensionOptionValues.None.ValueOf(null!));
}
