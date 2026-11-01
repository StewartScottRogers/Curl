using Curl.Core;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.SocketIo;

/// <summary>Checks the extension's description, its handler factory and its registration in <c>Curl.Extension.Composition</c>.</summary>
[TestClass]
public sealed class SocketIoExtensionTests
{
    private static readonly ProtocolHandlerServices Services = new(new ScriptedConnector(ConnectResult.Refused("x")), null!, null!, null!);

    [TestMethod]
    public void Description_NamesBothSchemes() =>
        CollectionAssert.AreEqual(new[] { "socketio", "socketios" }, SocketIoExtension.Description.Schemes.ToArray());

    [TestMethod]
    public void CreateHandler_GivesTheSocketIoHandler() =>
        Assert.IsInstanceOfType<SocketIoProtocolHandler>(SocketIoExtension.CreateHandler(Services));

    [TestMethod]
    public void CreateHandler_WithoutServices_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => SocketIoExtension.CreateHandler(null!));

    [TestMethod]
    public void ExtensionProtocolHandlers_WithExtensionsOn_ServeSocketIo()
    {
        ProtocolDispatcher dispatcher = new(ExtensionProtocolHandlers.For(_ => null)!(Services));

        Assert.IsTrue(dispatcher.Serves("socketio"));
        Assert.IsTrue(dispatcher.Serves("socketios"));
    }

    [TestMethod]
    [DataRow("socketio://example.com/", "socketio")]
    [DataRow("socketios://example.com/", "socketios")]
    public async Task Dispatch_WithCurlExtensions0_FailsAsCurlFailsAnUnsupportedScheme(string url, string scheme)
    {
        Assert.IsNull(ExtensionProtocolHandlers.For(name => name == "CURL_EXTENSIONS" ? "0" : null));
        ProtocolDispatcher dispatcher = new([]);

        TransferResult result = await dispatcher.DispatchAsync(new TransferContext { Url = CurlUrl.Parse(url), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual($"Protocol \"{scheme}\" not supported", result.ErrorMessage);
    }
}
