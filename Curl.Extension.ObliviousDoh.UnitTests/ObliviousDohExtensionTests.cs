using System.Net;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Resolution;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.ObliviousDoh;

[TestClass]
public sealed class ObliviousDohExtensionTests
{
    private static readonly IDnsResolver CurlResolver = new FixedResolver();

    [TestMethod]
    public void ActivateResolver_WithoutTheOptions_GivesNoHook() =>
        Assert.IsNull(ObliviousDohExtension.ActivateResolver(ExtensionOptionValues.None));

    [TestMethod]
    public void ActivateResolver_BothOptions_GivesTheHookForTheProxyAndTarget()
    {
        var hook = (ObliviousDohResolverHook)ObliviousDohExtension.ActivateResolver(Values("https://proxy.example/p", "https://target.example/dns-query"))!;

        Assert.AreEqual(new Uri("https://proxy.example/p"), hook.Endpoints.Proxy);
        Assert.AreEqual(new Uri("https://target.example/dns-query"), hook.Endpoints.Target);
        Assert.AreSame(ObliviousDohExtension.ProxyOption, hook.Option);
        Assert.IsInstanceOfType<ObliviousDohResolver>(hook.ResolverOver(CurlResolver));
        Assert.ThrowsExactly<ArgumentNullException>(() => hook.ResolverOver(null!));
    }

    [TestMethod]
    [DataRow("https://proxy.example/p", null, "--odoh-proxy needs --odoh-target")]
    [DataRow(null, "https://target.example/dns-query", "--odoh-target needs --odoh-proxy")]
    [DataRow("ftp://proxy.example/p", "https://target.example/dns-query", "--odoh-proxy needs an http or https URL")]
    [DataRow("https://proxy.example/p", "target.example", "--odoh-target needs an http or https URL")]
    public void ActivateResolver_OneOptionOrNotAUrl_Throws(string? proxy, string? target, string message) =>
        Assert.Contains(message, Assert.ThrowsExactly<ArgumentException>(() => ObliviousDohExtension.ActivateResolver(Values(proxy, target))).Message);

    [TestMethod]
    public void ActivateResolver_NullValues_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ObliviousDohExtension.ActivateResolver(null!));

    [TestMethod]
    [DataRow("https://proxy.example/dns-query{?targethost,targetpath}")]
    [DataRow("https://proxy.example/dns-query")]
    public void ProxyRequestUri_AddsTargetHostAndTargetPathAsRfc9230Section4_1(string proxy) =>
        Assert.AreEqual(
            "https://proxy.example/dns-query?targethost=dnstarget.example&targetpath=/dns-query",
            ObliviousDohEndpoints.Parse(proxy, "https://dnstarget.example/dns-query").ProxyRequestUri.OriginalString);

    [TestMethod]
    public void ProxyRequestUri_ProxyWithAQuery_AppendsTheVariables() =>
        Assert.AreEqual(
            "http://proxy.example/p?k=1&targethost=t.example%3A8443&targetpath=/q",
            ObliviousDohEndpoints.Parse("http://proxy.example/p?k=1", "https://t.example:8443/q").ProxyRequestUri.OriginalString);

    [TestMethod]
    public void ConfigsUri_IsTheWellKnownPathOnTheTargetsOrigin() =>
        Assert.AreEqual(new Uri("https://t.example:8443/.well-known/odohconfigs"), ObliviousDohEndpoints.Parse("https://p.example/", "https://t.example:8443/dns-query").ConfigsUri);

    [TestMethod]
    [DataRow("odoh-proxy")]
    [DataRow("odoh-target")]
    public void CommandLineOptions_WithExtensionsOff_LeaveTheOptionOut_SoTheParserRefusesItAsUnknown(string name) =>
        Assert.IsFalse(ExtensionCommandLineOptions.For(variable => variable == "CURL_EXTENSIONS" ? "0" : null, _ => false).Any(option => option.Name == name));

    [TestMethod]
    [DataRow("odoh-proxy")]
    [DataRow("odoh-target")]
    public void CommandLineOptions_WithExtensionsOn_HoldTheOptionWithAValue(string name) =>
        Assert.AreEqual(ExtensionOptionArgument.Value, ExtensionCommandLineOptions.For(_ => null, _ => false).Single(option => option.Name == name).Argument);

    [TestMethod]
    public void ResolverHookCatalogue_WithoutTheOptions_KeepsCurlsResolver()
    {
        ResolverHooks hooks = ResolverHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionResolverHookCatalogue.Entries, dohUrlGiven: false);

        Assert.IsNull(hooks.Hook);
        Assert.AreSame(CurlResolver, hooks.ResolverOver(CurlResolver));
    }

    [TestMethod]
    public void ResolverHookCatalogue_WithTheOptions_ActivatesTheObliviousDohHook() =>
        Assert.IsInstanceOfType<ObliviousDohResolverHook>(ResolverHooks.ActivatedBy(Values("https://p.example/", "https://t.example/"), ExtensionResolverHookCatalogue.Entries, dohUrlGiven: false).Hook);

    [TestMethod]
    public void ResolverHookCatalogue_BesideDohUrl_ThrowsNamingBoth() =>
        Assert.Contains("--odoh-proxy and --doh-url", Assert.ThrowsExactly<ArgumentException>(() => ResolverHooks.ActivatedBy(Values("https://p.example/", "https://t.example/"), ExtensionResolverHookCatalogue.Entries, dohUrlGiven: true)).Message);

    [TestMethod]
    public void Listing_NamesTheExtensionAndItsOptions()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("oblivious-doh", StringComparison.Ordinal)));
        string markdown = ExtensionListing.Markdown(_ => null);
        Assert.Contains("--odoh-proxy", markdown);
        Assert.Contains("--odoh-target", markdown);
        Assert.AreEqual(new Uri("https://www.rfc-editor.org/rfc/rfc9230"), ObliviousDohExtension.Description.Specification);
    }

    private static ExtensionOptionValues Values(string? proxy, string? target)
    {
        var values = new Dictionary<string, string>();
        if (proxy is not null)
        {
            values["odoh-proxy"] = proxy;
        }

        if (target is not null)
        {
            values["odoh-target"] = target;
        }

        return new ExtensionOptionValues(values);
    }

    private sealed class FixedResolver : IDnsResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Loopback]);
    }
}
