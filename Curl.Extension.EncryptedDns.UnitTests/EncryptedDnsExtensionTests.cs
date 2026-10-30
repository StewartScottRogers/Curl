using System.Net;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Resolution;
using Curl.Extension.Composition;
using Curl.Extension.EncryptedDns.DnsOverQuic;
using Curl.Extension.EncryptedDns.DnsOverTls;

namespace Curl.Extension.EncryptedDns;

[TestClass]
public sealed class EncryptedDnsExtensionTests
{
    private static readonly FixedDnsResolver CurlResolver = new(IPAddress.Parse("192.0.2.9"));

    [TestMethod]
    public void ActivateDnsOverTls_WithoutTheOption_GivesNoHook() =>
        Assert.IsNull(EncryptedDnsExtension.ActivateDnsOverTls(ExtensionOptionValues.None));

    [TestMethod]
    public void ActivateDnsOverQuic_WithoutTheOption_GivesNoHook() =>
        Assert.IsNull(EncryptedDnsExtension.ActivateDnsOverQuic(ExtensionOptionValues.None));

    [TestMethod]
    public void ActivateDnsOverTls_HostOnly_UsesPort853FromRfc7858Section3_1()
    {
        var hook = (DnsOverTlsResolverHook)EncryptedDnsExtension.ActivateDnsOverTls(ValuesOf("dns-over-tls", "dot.example"))!;

        Assert.AreEqual(new DnsOverTlsServer("dot.example", 853), hook.Server);
        Assert.AreSame(EncryptedDnsExtension.DnsOverTlsOption, hook.Option);
    }

    [TestMethod]
    public void ActivateDnsOverQuic_HostOnly_UsesPort853FromRfc9250Section4_1()
    {
        var hook = (DnsOverQuicResolverHook)EncryptedDnsExtension.ActivateDnsOverQuic(ValuesOf("dns-over-quic", "doq.example"))!;

        Assert.AreEqual(new DnsOverQuicServer("doq.example", 853), hook.Server);
        Assert.AreSame(EncryptedDnsExtension.DnsOverQuicOption, hook.Option);
    }

    [TestMethod]
    public void ActivateDnsOverTls_HostAndPort_UsesThePort() =>
        Assert.AreEqual(8853, ((DnsOverTlsResolverHook)EncryptedDnsExtension.ActivateDnsOverTls(ValuesOf("dns-over-tls", "dot.example:8853"))!).Server.Port);

    [TestMethod]
    public void ActivateDnsOverQuic_BracketedAddressAndPort_UsesTheAddressAndPort() =>
        Assert.AreEqual(new DnsOverQuicServer("2001:db8::53", 784), ((DnsOverQuicResolverHook)EncryptedDnsExtension.ActivateDnsOverQuic(ValuesOf("dns-over-quic", "[2001:db8::53]:784"))!).Server);

    [TestMethod]
    public void ActivateDnsOverTls_NotHostAndPort_Throws() =>
        Assert.ThrowsExactly<ArgumentException>(() => EncryptedDnsExtension.ActivateDnsOverTls(ValuesOf("dns-over-tls", "dot.example:0")));

    [TestMethod]
    public void ActivateDnsOverQuic_NotHostAndPort_Throws() =>
        Assert.ThrowsExactly<ArgumentException>(() => EncryptedDnsExtension.ActivateDnsOverQuic(ValuesOf("dns-over-quic", "doq.example:x")));

    [TestMethod]
    public void ActivateDnsOverTls_NullValues_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => EncryptedDnsExtension.ActivateDnsOverTls(null!));

    [TestMethod]
    public void ActivateDnsOverQuic_NullValues_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => EncryptedDnsExtension.ActivateDnsOverQuic(null!));

    [TestMethod]
    [DataRow("dns-over-tls")]
    [DataRow("dns-over-quic")]
    public void CommandLineOptions_WithExtensionsOff_LeaveTheOptionOut_SoTheParserRefusesItAsUnknown(string name) =>
        Assert.IsFalse(ExtensionCommandLineOptions.For(variable => variable == "CURL_EXTENSIONS" ? "0" : null, _ => false).Any(option => option.Name == name));

    [TestMethod]
    [DataRow("dns-over-tls")]
    [DataRow("dns-over-quic")]
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
    public void ResolverHookCatalogue_WithDnsOverTls_ActivatesTheDnsOverTlsHook() =>
        Assert.IsInstanceOfType<DnsOverTlsResolverHook>(ResolverHooks.ActivatedBy(ValuesOf("dns-over-tls", "dot.example"), ExtensionResolverHookCatalogue.Entries, dohUrlGiven: false).Hook);

    [TestMethod]
    public void ResolverHookCatalogue_WithDnsOverQuic_ActivatesTheDnsOverQuicHook() =>
        Assert.IsInstanceOfType<DnsOverQuicResolverHook>(ResolverHooks.ActivatedBy(ValuesOf("dns-over-quic", "doq.example"), ExtensionResolverHookCatalogue.Entries, dohUrlGiven: false).Hook);

    [TestMethod]
    public void ResolverHookCatalogue_BothOptions_ThrowsNamingBoth()
    {
        var values = new ExtensionOptionValues(new Dictionary<string, string> { ["dns-over-tls"] = "dot.example", ["dns-over-quic"] = "doq.example" });

        ArgumentException error = Assert.ThrowsExactly<ArgumentException>(() => ResolverHooks.ActivatedBy(values, ExtensionResolverHookCatalogue.Entries, dohUrlGiven: false));

        Assert.Contains("--dns-over-tls and --dns-over-quic", error.Message);
    }

    [TestMethod]
    public void ResolverHookCatalogue_BesideDohUrl_ThrowsNamingBoth()
    {
        ArgumentException error = Assert.ThrowsExactly<ArgumentException>(() => ResolverHooks.ActivatedBy(ValuesOf("dns-over-tls", "dot.example"), ExtensionResolverHookCatalogue.Entries, dohUrlGiven: true));

        Assert.Contains("--dns-over-tls and --doh-url", error.Message);
    }

    [TestMethod]
    public void Listing_NamesTheExtensionAndItsOptions()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("encrypted-dns", StringComparison.Ordinal)));
        string markdown = ExtensionListing.Markdown(_ => null);
        Assert.Contains("--dns-over-tls", markdown);
        Assert.Contains("--dns-over-quic", markdown);
        Assert.AreEqual(new Uri("https://www.rfc-editor.org/rfc/rfc7858"), EncryptedDnsExtension.Description.Specification);
    }

    private static ExtensionOptionValues ValuesOf(string name, string value) => new(new Dictionary<string, string> { [name] = value });
}
