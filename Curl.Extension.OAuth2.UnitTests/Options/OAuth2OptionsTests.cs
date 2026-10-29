using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;

namespace Curl.Extension.OAuth2.Options;

/// <summary>Pins how the OAuth2 options are registered and turn the hook on (ADR-1075).</summary>
[TestClass]
public sealed class OAuth2OptionsTests
{
    [TestMethod]
    public void ActivatedBy_AFlowOptionGiven_GivesTheHook()
    {
        var hook = OAuth2Options.ActivatedBy(Given(("oauth2-client-credentials", "https://server.example.com/token"), ("oauth2-client-id", "s6BhdRkqt3")));

        Assert.IsInstanceOfType<OAuth2TransferHook>(hook);
        Assert.IsNotNull(((OAuth2TransferHook)hook).Configuration);
    }

    [TestMethod]
    public void ActivatedBy_NoFlowOptionGiven_GivesNone()
    {
        Assert.IsNull(OAuth2Options.ActivatedBy(ExtensionOptionValues.None));
        Assert.IsNull(OAuth2Options.ActivatedBy(Given(("oauth2-client-id", "s6BhdRkqt3"))));
        Assert.ThrowsExactly<ArgumentNullException>(() => OAuth2Options.ActivatedBy(null!));
    }

    [TestMethod]
    public void ActivatedBy_WrongOptions_GivesAHookWithTheErrorAndNoConfiguration()
    {
        var hook = OAuth2Options.ActivatedBy(Given(("oauth2-device", "https://server.example.com/token")));

        Assert.IsInstanceOfType<OAuth2TransferHook>(hook);
        Assert.IsNull(((OAuth2TransferHook)hook).Configuration);
    }

    [TestMethod]
    public void ActivatedBy_NoTokenCacheGiven_UsesTheDefaultDirectory()
    {
        var hook = (OAuth2TransferHook)OAuth2Options.ActivatedBy(
            Given(("oauth2-client-credentials", "https://server.example.com/token"), ("oauth2-client-id", "s6BhdRkqt3")),
            TextWriter.Null,
            name => name == "CURL_HOME" ? "/home/user" : null,
            count => new byte[count])!;

        Assert.AreEqual("/home/user", hook.Configuration?.CacheDirectory);
    }

    [TestMethod]
    public void Composition_RegistersEveryOptionOnlyWhileExtensionsAreOn()
    {
        var on = ExtensionCommandLineOptions.For(_ => null, _ => false);
        var off = ExtensionCommandLineOptions.For(name => name == ExtensionProtocolHandlers.SwitchVariable ? "0" : null, _ => false);

        foreach (var option in OAuth2Options.All)
        {
            Assert.Contains(option, on);
            Assert.DoesNotContain(option, off);
        }
    }

    [TestMethod]
    public void Composition_ActivatesTheHookOnlyWhenAFlowOptionIsGiven()
    {
        var given = Given(("oauth2-client-credentials", "https://server.example.com/token"), ("oauth2-client-id", "s6BhdRkqt3"));

        Assert.IsTrue(TransferHooks.ActivatedBy(given, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<OAuth2TransferHook>().Any());
        Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<OAuth2TransferHook>().Any());
    }

    [TestMethod]
    public void Composition_ListsTheDescriptionInExtensionsAndAiHelp()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("oauth2", StringComparison.Ordinal)));
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), OAuth2Options.Description.Summary);
        StringAssert.Contains(ExtensionListing.Markdown(_ => null), "--oauth2-pkce");
    }

    internal static ExtensionOptionValues Given(params (string Name, string Value)[] options) =>
        new(options.ToDictionary(option => option.Name, option => option.Value));
}
