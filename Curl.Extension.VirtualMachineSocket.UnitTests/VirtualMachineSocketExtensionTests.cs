using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.VirtualMachineSocket;

/// <summary>Checks the <c>--vm-socket</c> option's activation and its registration in <c>Curl.Extension.Composition</c>.</summary>
[TestClass]
public sealed class VirtualMachineSocketExtensionTests
{
    private static readonly IConnector CurlConnector = new VirtualMachineSocketConnector(new VsockAddress(2, 1), new FakeVirtualMachineSocketOpener(), httpOverTlsApplicationProtocols: ["h2", "http/1.1"]);

    [TestMethod]
    public void Activate_WithoutTheOption_GivesNoHook()
    {
        Assert.IsNull(VirtualMachineSocketExtension.Activate(ExtensionOptionValues.None, new FakeVirtualMachineSocketOpener()));
        Assert.IsNull(VirtualMachineSocketExtension.Activate(ExtensionOptionValues.None));
    }

    [TestMethod]
    public async Task Activate_WithAVsockAddress_GivesAHookWhoseConnectorOpensThatAddress()
    {
        FakeVirtualMachineSocketOpener opener = new();

        IConnectionHook hook = VirtualMachineSocketExtension.Activate(ValuesOf("host:8080"), opener)!;
        ConnectResult result = await hook.ConnectorOver(CurlConnector).ConnectAsync(new ConnectTarget("localhost", 80, UseTls: false), CancellationToken.None);
        await result.Connection!.DisposeAsync();

        Assert.AreSame(VirtualMachineSocketExtension.Option, hook.Option);
        Assert.AreEqual(new VsockAddress(VsockAddress.HostCid, 8080), ((VirtualMachineSocketConnectionHook)hook).Address);
        CollectionAssert.AreEqual(new[] { new VsockAddress(2, 8080) }, opener.Opened);
        Assert.AreNotSame(CurlConnector, hook.ConnectorOver(CurlConnector));
    }

    [TestMethod]
    public async Task Activate_WithCurlsTlsProvider_GivesAConnectorRunningTlsOfferingCurlsAlpnList()
    {
        RecordedHandshakeTlsProvider tls = new([0x16], 0);

        IConnectionHook hook = VirtualMachineSocketExtension.Activate(ValuesOf("parent:3049197c-facb-11e6-bd58-64006a7986d3"), new FakeVirtualMachineSocketOpener())!;
        ConnectResult result = await hook.ConnectorOver(CurlConnector, tls).ConnectAsync(new ConnectTarget("localhost", 443, UseTls: true) { PoolScheme = "https" }, CancellationToken.None);
        await using IConnection connection = result.Connection!;

        Assert.IsTrue(connection.IsSecure);
        CollectionAssert.AreEqual(new[] { "h2", "http/1.1" }, tls.ApplicationProtocols!.ToArray());
        Assert.ThrowsExactly<ArgumentNullException>(() => hook.ConnectorOver(null!, tls));
    }

    [TestMethod]
    public void Activate_InProduction_GivesAHookOverTheSystemOpener() =>
        Assert.IsInstanceOfType<VirtualMachineSocketConnectionHook>(VirtualMachineSocketExtension.Activate(ValuesOf("3:22")));

    [TestMethod]
    public void Activate_WithTextThatIsNotAnAddress_IsTheCommandLineError()
    {
        ArgumentException error = Assert.ThrowsExactly<ArgumentException>(() => VirtualMachineSocketExtension.Activate(ValuesOf("localhost:80x"), new FakeVirtualMachineSocketOpener()));

        Assert.StartsWith("--vm-socket needs a vsock CID:port or a Hyper-V VM:service GUID pair, not 'localhost:80x'", error.Message);
    }

    [TestMethod]
    public void Activate_NullValues_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => VirtualMachineSocketExtension.Activate(null!, new FakeVirtualMachineSocketOpener()));

    [TestMethod]
    public void CommandLineOptions_WithExtensionsOff_LeaveVmSocketOut_SoTheParserRefusesItAsUnknown() =>
        Assert.IsFalse(ExtensionCommandLineOptions.For(name => name == "CURL_EXTENSIONS" ? "0" : null, _ => false).Any(option => option.Name == "vm-socket"));

    [TestMethod]
    public void CommandLineOptions_WithExtensionsOn_HoldVmSocketWithAValue()
    {
        ExtensionOption option = ExtensionCommandLineOptions.For(_ => null, _ => false).Single(option => option.Name == "vm-socket");

        Assert.AreSame(VirtualMachineSocketExtension.Option, option);
        Assert.AreEqual(ExtensionOptionArgument.Value, option.Argument);
    }

    [TestMethod]
    public void ConnectionHookCatalogue_WithoutTheOption_KeepsCurlsConnector()
    {
        ConnectionHooks hooks = ConnectionHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionConnectionHookCatalogue.Entries);

        Assert.IsNull(hooks.Hook);
        Assert.AreSame(CurlConnector, hooks.ConnectorOver(CurlConnector));
    }

    [TestMethod]
    public void ConnectionHookCatalogue_WithTheOption_ActivatesTheVirtualMachineSocketHook() =>
        Assert.IsInstanceOfType<VirtualMachineSocketConnectionHook>(ConnectionHooks.ActivatedBy(ValuesOf("3:22"), ExtensionConnectionHookCatalogue.Entries).Hook);

    [TestMethod]
    public void Listing_NamesTheExtensionAndItsOption()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("vm-socket", StringComparison.Ordinal)));
        Assert.Contains("--vm-socket", ExtensionListing.Markdown(_ => null));
        Assert.AreEqual(new Uri("https://man7.org/linux/man-pages/man7/vsock.7.html"), VirtualMachineSocketExtension.Description.Specification);
    }

    private static ExtensionOptionValues ValuesOf(string address) => new(new Dictionary<string, string> { ["vm-socket"] = address });
}
