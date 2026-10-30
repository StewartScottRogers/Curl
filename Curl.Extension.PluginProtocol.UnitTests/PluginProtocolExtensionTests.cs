using Curl.Core;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.PluginProtocol;

/// <summary>Checks the extension's description, its handler factory and its registration in <c>Curl.Extension.Composition</c>.</summary>
[TestClass]
public sealed class PluginProtocolExtensionTests
{
    private static readonly ProtocolHandlerServices Services = new(null!, null!, null!, null!);

    [TestMethod]
    public void Description_NamesNoFixedSchemeAndTheSpecification()
    {
        Assert.AreEqual("plugin-protocol", PluginProtocolExtension.Description.Name);
        Assert.IsEmpty(PluginProtocolExtension.Description.Schemes);
        Assert.AreEqual("https://git-scm.com/docs/gitremote-helpers", PluginProtocolExtension.Description.Specification.ToString());
    }

    [TestMethod]
    public void CreateHandler_GivesTheHelperHandler() =>
        Assert.IsInstanceOfType<HelperProtocolHandler>(PluginProtocolExtension.CreateHandler(Services, []));

    [TestMethod]
    public void CreateHandler_WithoutServices_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => PluginProtocolExtension.CreateHandler(null!, []));

    [TestMethod]
    public void CreateHandler_WithoutExtensionSchemes_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => PluginProtocolExtension.CreateHandler(Services, null!));

    [TestMethod]
    public void Create_ServesEachHelpersSchemeButNeverCurlsOrAnotherExtensions()
    {
        Dictionary<string, string?> environment = new() { ["PATH"] = "/bin" };
        string[] files = ["/bin/curl-proto-demo", "/bin/curl-proto-https", "/bin/curl-proto-nats"];

        HelperProtocolHandler handler = PluginProtocolExtension.Create(name => environment.GetValueOrDefault(name), false, _ => files, new ScriptedHelperProcessStarter(null), ["nats"]);

        CollectionAssert.AreEqual(new[] { "demo" }, handler.SupportedSchemes.ToArray());
    }

    [TestMethod]
    public void CurlSchemes_HoldEverySchemeCurlLists()
    {
        string[] versionProtocols = "dict file ftp ftps gopher gophers http https imap imaps ipfs ipns ldap ldaps mqtt mqtts pop3 pop3s rtsp scp sftp smtp smtps telnet tftp ws wss".Split(' ');

        CollectionAssert.IsSubsetOf(versionProtocols, PluginProtocolExtension.CurlSchemes.ToArray());
    }

    [TestMethod]
    public void ExtensionProtocolHandlers_WithExtensionsOn_BuildADispatcherWithoutAClash()
    {
        ProtocolDispatcher dispatcher = new(ExtensionProtocolHandlers.For(_ => null)!(Services));

        Assert.IsTrue(dispatcher.Serves("nats"));
    }

    [TestMethod]
    public async Task Dispatch_WithCurlExtensions0_FailsAsCurlFailsAnUnsupportedScheme()
    {
        Assert.IsNull(ExtensionProtocolHandlers.For(name => name == "CURL_EXTENSIONS" ? "0" : null));
        ProtocolDispatcher dispatcher = new([]);

        TransferResult result = await dispatcher.DispatchAsync(new TransferContext { Url = CurlUrl.Parse("demo://host/a"), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual("Protocol \"demo\" not supported", result.ErrorMessage);
    }
}
