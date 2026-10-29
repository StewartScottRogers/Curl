using Curl.Extension.Abstractions;

namespace Curl.Extension.Composition;

[TestClass]
public sealed class ExtensionCommandLineOptionsTests
{
    private static readonly ExtensionOption CacheDirectory = new("cache-dir", ExtensionOptionArgument.Value, "Keep responses in <dir>");
    private static readonly ExtensionOption Sse = new("sse", ExtensionOptionArgument.None, "Read the response as server-sent events");

    [TestMethod]
    public void For_WithExtensionsOn_GivesTheRegisteredOptions()
    {
        IReadOnlyList<ExtensionOption> options = ExtensionCommandLineOptions.For(_ => null, _ => false);

        CollectionAssert.AreEqual(ExtensionOptionCatalogue.Entries.ToArray(), options.ToArray());
    }

    [TestMethod]
    public void For_WithExtensionsOff_GivesNone()
    {
        IReadOnlyList<ExtensionOption> options = ExtensionCommandLineOptions.For(name => name == "CURL_EXTENSIONS" ? "0" : null, _ => true);

        Assert.IsEmpty(options);
    }

    [TestMethod]
    public void For_WithNoEnvironmentReader_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ExtensionCommandLineOptions.For(null!, _ => false));

    [TestMethod]
    public void For_WithNoCurlOptionCheck_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ExtensionCommandLineOptions.For(_ => null, null!));

    [TestMethod]
    public void Checked_OptionsWithDistinctNewNames_GivesThemUnchanged()
    {
        ExtensionOption[] registered = [CacheDirectory, Sse];

        IReadOnlyList<ExtensionOption> options = ExtensionCommandLineOptions.Checked(registered, _ => false);

        Assert.AreSame(registered, options);
    }

    [TestMethod]
    public void Checked_AnOptionNamedAsACurlOption_FailsNamingBoth()
    {
        ExtensionOption output = new("output", ExtensionOptionArgument.Value, "Write somewhere else");

        InvalidOperationException failure = Assert.ThrowsExactly<InvalidOperationException>(
            () => ExtensionCommandLineOptions.Checked([Sse, output], name => name == "output"));

        Assert.AreEqual("The extension option --output (\"Write somewhere else\") has the name of curl's option --output.", failure.Message);
    }

    [TestMethod]
    public void Checked_TwoOptionsOfOneName_FailsNamingBoth()
    {
        ExtensionOption otherCacheDirectory = new("cache-dir", ExtensionOptionArgument.Value, "Cache in <dir> too");

        InvalidOperationException failure = Assert.ThrowsExactly<InvalidOperationException>(
            () => ExtensionCommandLineOptions.Checked([CacheDirectory, otherCacheDirectory], _ => false));

        Assert.AreEqual("The extension options \"Keep responses in <dir>\" and \"Cache in <dir> too\" are both named --cache-dir.", failure.Message);
    }
}
