using Curl.Core;
using Curl.Extension.Abstractions;
using Curl.Extension.Composition;
using Curl.Extension.DnsQuery.Client;
using Curl.Extension.DnsQuery.UnitTests.Transport;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.DnsQuery.UnitTests.Client;

/// <summary>Checks the extension's description, options, hook, handler factory and its registration in <c>Curl.Extension.Composition</c>.</summary>
[TestClass]
public sealed class DnsQueryExtensionTests
{
    private static readonly ScriptedDnsServer Server = new((_, _) => [], []);
    private static readonly ProtocolHandlerServices Services = new(Server, Server, null!, null!);

    [TestMethod]
    public void Description_NamesTheDnsSchemeAndBothOptions()
    {
        Assert.AreEqual("dns", DnsQueryExtension.Description.Name);
        CollectionAssert.AreEqual(new[] { "dns" }, DnsQueryExtension.Description.Schemes.ToArray());
        CollectionAssert.AreEqual(new[] { DnsQueryExtension.TransportOption, DnsQueryExtension.JsonOption }, DnsQueryExtension.Description.Options.ToArray());
    }

    [TestMethod]
    public void Options_TakeAValueForTheTransportAndNoneForJson()
    {
        Assert.AreEqual(("dns-transport", ExtensionOptionArgument.Value), (DnsQueryExtension.TransportOption.Name, DnsQueryExtension.TransportOption.Argument));
        Assert.AreEqual(("dns-json", ExtensionOptionArgument.None), (DnsQueryExtension.JsonOption.Name, DnsQueryExtension.JsonOption.Argument));
        Assert.Contains("udp, tcp", DnsQueryExtension.TransportOption.Help);
    }

    [TestMethod]
    public void CreateHandler_GivesTheDnsHandler() =>
        Assert.IsInstanceOfType<DnsQueryProtocolHandler>(DnsQueryExtension.CreateHandler(Services));

    [TestMethod]
    public void CreateHandler_WithoutServices_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => DnsQueryExtension.CreateHandler(null!));

    [TestMethod]
    public void HookFor_NeitherOption_GivesNoHook() =>
        Assert.IsNull(DnsQueryExtension.HookFor(ExtensionOptionValues.None));

    [TestMethod]
    public void HookFor_WithoutValues_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => DnsQueryExtension.HookFor(null!));

    [TestMethod]
    [DataRow("dns-transport", "tcp", "tcp", false)]
    [DataRow("dns-json", "", "udp", true)]
    public async Task HookFor_EitherOption_HandsTheChoiceToTheHandler(string option, string value, string transport, bool json)
    {
        var values = new ExtensionOptionValues(new Dictionary<string, string> { [option] = value });
        ITransferContext? given = null;

        await DnsQueryExtension.HookFor(values)!.ExecuteAsync(ContextOf("dns://h/example.com"), context =>
        {
            given = context;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        var marked = (DnsQueryContext)given!;
        Assert.AreEqual((transport, json), (marked.Transport, marked.Json));
    }

    [TestMethod]
    public async Task HookFor_AnotherScheme_PassesTheTransferThroughUnchanged()
    {
        TransferContext original = ContextOf("http://h/");
        ITransferContext? given = null;

        await DnsQueryExtension.HookFor(new ExtensionOptionValues(new Dictionary<string, string> { ["dns-json"] = "" }))!.ExecuteAsync(original, context =>
        {
            given = context;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        Assert.AreSame(original, given);
    }

    [TestMethod]
    public void ExtensionProtocolHandlers_WithExtensionsOn_ServeDns() =>
        Assert.IsTrue(new ProtocolDispatcher(ExtensionProtocolHandlers.For(_ => null)!(Services)).Serves("dns"));

    [TestMethod]
    public void ExtensionListing_WithExtensionsOn_ListsDnsInBothForms()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("dns", StringComparison.Ordinal)));
        string markdown = ExtensionListing.Markdown(_ => null);
        Assert.Contains("Queries a DNS server with a dns:// URL", markdown);
        Assert.Contains("dns-transport", markdown);
    }

    [TestMethod]
    public async Task Dispatch_WithCurlExtensions0_FailsAsCurlFailsAnUnsupportedScheme()
    {
        Assert.IsNull(ExtensionProtocolHandlers.For(name => name == "CURL_EXTENSIONS" ? "0" : null));
        ProtocolDispatcher dispatcher = new([]);

        TransferResult result = await dispatcher.DispatchAsync(ContextOf("dns://192.0.2.53/example.com?type=A"));

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual("Protocol \"dns\" not supported", result.ErrorMessage);
    }

    private static TransferContext ContextOf(string url) => new() { Url = CurlUrl.Parse(url), Output = new MemoryStream() };
}
