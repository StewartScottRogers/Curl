using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.NamedPipe;

/// <summary>Checks the <c>--npipe</c> option's activation and its registration in <c>Curl.Extension.Composition</c>.</summary>
[TestClass]
public sealed class NamedPipeExtensionTests
{
    private static readonly IConnector CurlConnector = new NamedPipeConnector(new NamedPipeName(".", "curl"), new FakeNamedPipeOpener());

    [TestMethod]
    public void Activate_WithoutTheOption_GivesNoHook()
    {
        Assert.IsNull(NamedPipeExtension.Activate(ExtensionOptionValues.None, new FakeNamedPipeOpener()));
        Assert.IsNull(NamedPipeExtension.Activate(ExtensionOptionValues.None));
    }

    [TestMethod]
    public async Task Activate_WithAPipeName_GivesAHookWhoseConnectorOpensThatPipe()
    {
        FakeNamedPipeOpener opener = new();

        IConnectionHook hook = NamedPipeExtension.Activate(ValuesOf(@"\\.\pipe\docker_engine"), opener)!;
        ConnectResult result = await hook.ConnectorOver(CurlConnector).ConnectAsync(new ConnectTarget("localhost", 80, UseTls: false), CancellationToken.None);
        await result.Connection!.DisposeAsync();

        Assert.AreSame(NamedPipeExtension.Option, hook.Option);
        Assert.AreEqual(new NamedPipeName(".", "docker_engine"), ((NamedPipeConnectionHook)hook).Name);
        CollectionAssert.AreEqual(new[] { new NamedPipeName(".", "docker_engine") }, opener.Opened);
        Assert.AreNotSame(CurlConnector, hook.ConnectorOver(CurlConnector));
    }

    [TestMethod]
    public async Task Activate_WithAPipeNameAndCurlsTlsProvider_GivesAHookWhoseConnectorRunsTlsOverThePipe()
    {
        FakeNamedPipeOpener opener = new() { ServerBytes = [0x16, 0x03, 0x03] };
        RecordedHandshakeTlsProvider tls = new([0x16, 0x03, 0x01], 3);

        IConnectionHook hook = NamedPipeExtension.Activate(ValuesOf(@"\\.\pipe\docker_engine"), opener)!;
        ConnectResult result = await hook.ConnectorOver(CurlConnector, tls).ConnectAsync(new ConnectTarget("localhost", 443, UseTls: true), CancellationToken.None);
        await using IConnection connection = result.Connection!;

        Assert.IsTrue(connection.IsSecure);
        Assert.AreEqual("localhost", tls.TargetHost);
        CollectionAssert.AreEqual(new byte[] { 0x16, 0x03, 0x01 }, opener.LastPipe!.Written.ToArray());
    }

    [TestMethod]
    public async Task Activate_WithCurlsTlsProvider_GivesAConnectorOfferingCurlsConnectorsAlpnList()
    {
        string[] h2ThenHttp11 = ["h2", "http/1.1"];
        RecordedHandshakeTlsProvider tls = new([0x16], 0);
        IConnector curls = new NamedPipeConnector(new NamedPipeName(".", "curl"), new FakeNamedPipeOpener(), httpOverTlsApplicationProtocols: h2ThenHttp11);

        IConnectionHook hook = NamedPipeExtension.Activate(ValuesOf(@"\\.\pipe\docker_engine"), new FakeNamedPipeOpener())!;
        ConnectResult result = await hook.ConnectorOver(curls, tls).ConnectAsync(new ConnectTarget("localhost", 443, UseTls: true) { PoolScheme = "https" }, CancellationToken.None);
        await result.Connection!.DisposeAsync();

        CollectionAssert.AreEqual(h2ThenHttp11, tls.ApplicationProtocols!.ToArray());
        Assert.ThrowsExactly<ArgumentNullException>(() => hook.ConnectorOver(null!, tls));
    }

    [TestMethod]
    public void Activate_InProduction_GivesAHookOverTheSystemOpener() =>
        Assert.IsInstanceOfType<NamedPipeConnectionHook>(NamedPipeExtension.Activate(ValuesOf(@"\\.\pipe\agent")));

    [TestMethod]
    public void Activate_WithTextThatIsNotAPipeName_IsTheCommandLineError()
    {
        ArgumentException error = Assert.ThrowsExactly<ArgumentException>(() => NamedPipeExtension.Activate(ValuesOf("/var/run/docker.sock"), new FakeNamedPipeOpener()));

        Assert.StartsWith(@"--npipe needs a named pipe such as \\.\pipe\name, not '/var/run/docker.sock'", error.Message);
    }

    [TestMethod]
    public void Activate_NullValues_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => NamedPipeExtension.Activate(null!, new FakeNamedPipeOpener()));

    [TestMethod]
    public void CommandLineOptions_WithExtensionsOff_LeaveNpipeOut_SoTheParserRefusesItAsUnknown() =>
        Assert.IsFalse(ExtensionCommandLineOptions.For(name => name == "CURL_EXTENSIONS" ? "0" : null, _ => false).Any(option => option.Name == "npipe"));

    [TestMethod]
    public void CommandLineOptions_WithExtensionsOn_HoldNpipeWithAValue()
    {
        ExtensionOption option = ExtensionCommandLineOptions.For(_ => null, _ => false).Single(option => option.Name == "npipe");

        Assert.AreSame(NamedPipeExtension.Option, option);
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
    public void ConnectionHookCatalogue_WithTheOption_ActivatesTheNamedPipeHook() =>
        Assert.IsInstanceOfType<NamedPipeConnectionHook>(ConnectionHooks.ActivatedBy(ValuesOf(@"\\.\pipe\agent"), ExtensionConnectionHookCatalogue.Entries).Hook);

    [TestMethod]
    public void Listing_NamesTheExtensionAndItsOption()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("npipe", StringComparison.Ordinal)));
        Assert.Contains("--npipe", ExtensionListing.Markdown(_ => null));
        Assert.AreEqual(new Uri("https://learn.microsoft.com/windows/win32/ipc/pipe-names"), NamedPipeExtension.Description.Specification);
    }

    private static ExtensionOptionValues ValuesOf(string pipe) => new(new Dictionary<string, string> { ["npipe"] = pipe });
}
