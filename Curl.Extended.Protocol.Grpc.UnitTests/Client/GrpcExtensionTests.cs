using Curl.Core;
using Curl.Extension.Abstractions;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Grpc.Client;

/// <summary>Checks the extension's description, option, hook and handler factory and its registration in <c>Curl.Extension.Composition</c>.</summary>
[TestClass]
public sealed class GrpcExtensionTests
{
    private static readonly ProtocolHandlerServices Services = new(new ScriptedConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "x")), null!, null!, null!);

    [TestMethod]
    public void Description_NamesTheGrpcAndGrpcsSchemesAndTheGrpcProtoOption()
    {
        CollectionAssert.AreEqual(new[] { "grpc", "grpcs" }, GrpcExtension.Description.Schemes.ToArray());
        Assert.AreEqual("grpc", GrpcExtension.Description.Name);
        Assert.AreEqual("grpc-proto", GrpcExtension.ProtoOption.Name);
        Assert.AreEqual(ExtensionOptionArgument.Value, GrpcExtension.ProtoOption.Argument);
    }

    [TestMethod]
    public void CreateHandler_GivesTheGrpcHandler() =>
        Assert.IsInstanceOfType<GrpcProtocolHandler>(GrpcExtension.CreateHandler(Services));

    [TestMethod]
    public void CreateHandler_WithoutServices_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => GrpcExtension.CreateHandler(null!));

    [TestMethod]
    public void HookFor_WithGrpcProto_GivesTheHook() =>
        Assert.IsInstanceOfType<GrpcProtoTransferHook>(GrpcExtension.HookFor(new ExtensionOptionValues(new Dictionary<string, string> { ["grpc-proto"] = "a.proto" })));

    [TestMethod]
    public void HookFor_WithoutGrpcProto_GivesNone() =>
        Assert.IsNull(GrpcExtension.HookFor(ExtensionOptionValues.None));

    [TestMethod]
    public void HookFor_WithoutValues_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => GrpcExtension.HookFor(null!));

    [TestMethod]
    public void ExtensionProtocolHandlers_WithExtensionsOn_ServeGrpcAndGrpcs()
    {
        var dispatcher = new ProtocolDispatcher(ExtensionProtocolHandlers.For(_ => null)!(Services));

        Assert.IsTrue(dispatcher.Serves("grpc"));
        Assert.IsTrue(dispatcher.Serves("grpcs"));
    }

    [TestMethod]
    public void ExtensionListing_WithExtensionsOn_ListsGrpcInBothForms()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("grpc", StringComparison.Ordinal)));
        Assert.Contains("Calls a gRPC method over HTTP/2", ExtensionListing.Markdown(_ => null));
        Assert.Contains("--grpc-proto", ExtensionListing.Markdown(_ => null));
    }

    [TestMethod]
    [DataRow("grpc")]
    [DataRow("grpcs")]
    public async Task Dispatch_WithCurlExtensions0_FailsAsCurlFailsAnUnsupportedScheme(string scheme)
    {
        Assert.IsNull(ExtensionProtocolHandlers.For(name => name == "CURL_EXTENSIONS" ? "0" : null));
        ProtocolDispatcher dispatcher = new([]);

        TransferResult result = await dispatcher.DispatchAsync(new TransferContext { Url = CurlUrl.Parse($"{scheme}://example.com/helloworld.Greeter/SayHello"), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual($"Protocol \"{scheme}\" not supported", result.ErrorMessage);
    }
}
