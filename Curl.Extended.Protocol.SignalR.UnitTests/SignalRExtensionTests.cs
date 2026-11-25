using Curl.Core;
using Curl.Extended.Protocol.SignalR.Client;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.SignalR;

/// <summary>Checks the extension's description, its handler factory and its registration in <c>Curl.Extension.Composition</c>.</summary>
[TestClass]
public sealed class SignalRExtensionTests
{
    private static readonly ProtocolHandlerServices Services = new(new ScriptedConnector(), null!, null!, null!);

    [TestMethod]
    public void Description_NamesBothSchemesAndNoOption()
    {
        CollectionAssert.AreEqual(new[] { "signalr", "signalrs" }, SignalRExtension.Description.Schemes.ToArray());
        Assert.IsEmpty(SignalRExtension.Description.Options);
    }

    [TestMethod]
    public void CreateHandler_GivesTheSignalRHandler() =>
        Assert.IsInstanceOfType<SignalRProtocolHandler>(SignalRExtension.CreateHandler(Services));

    [TestMethod]
    public void CreateHandler_WithoutServices_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => SignalRExtension.CreateHandler(null!));

    [TestMethod]
    public void ExtendedProtocolHandlers_WithExtensionsOn_ServeSignalR()
    {
        ProtocolDispatcher dispatcher = new(ExtendedProtocolHandlers.For(_ => null)!(Services));

        Assert.IsTrue(dispatcher.Serves("signalr"));
        Assert.IsTrue(dispatcher.Serves("signalrs"));
    }

    [TestMethod]
    [DataRow("signalr://example.com/hub", "signalr")]
    [DataRow("signalrs://example.com/hub", "signalrs")]
    public async Task Dispatch_WithCurlExtensions0_FailsAsCurlFailsAnUnsupportedScheme(string url, string scheme)
    {
        Assert.IsNull(ExtendedProtocolHandlers.For(name => name == "CURL_EXTENSIONS" ? "0" : null));
        ProtocolDispatcher dispatcher = new([]);

        TransferResult result = await dispatcher.DispatchAsync(new TransferContext { Url = CurlUrl.Parse(url), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual($"Protocol \"{scheme}\" not supported", result.ErrorMessage);
    }
}
