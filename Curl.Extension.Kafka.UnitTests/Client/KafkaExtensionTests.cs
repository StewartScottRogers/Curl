using Curl.Core;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Kafka.Client;

/// <summary>Checks the extension's description, its handler factory and its registration in <c>Curl.Extension.Composition</c>.</summary>
[TestClass]
public sealed class KafkaExtensionTests
{
    private static readonly ProtocolHandlerServices Services = new(new ScriptedKafkaConnector(ConnectResult.Refused("x")), null!, null!, null!);

    [TestMethod]
    public void Description_NamesTheKafkaSchemes()
    {
        CollectionAssert.AreEqual(new[] { "kafka", "kafkas" }, KafkaExtension.Description.Schemes.ToArray());
        Assert.AreEqual("kafka", KafkaExtension.Description.Name);
    }

    [TestMethod]
    public void CreateHandler_GivesTheKafkaHandler() =>
        Assert.IsInstanceOfType<KafkaProtocolHandler>(KafkaExtension.CreateHandler(Services));

    [TestMethod]
    public void CreateHandler_WithoutServices_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => KafkaExtension.CreateHandler(null!));

    [TestMethod]
    public void ExtensionProtocolHandlers_WithExtensionsOn_ServeKafkaAndKafkas()
    {
        ProtocolDispatcher dispatcher = new(ExtensionProtocolHandlers.For(_ => null)!(Services));

        Assert.IsTrue(dispatcher.Serves("kafka"));
        Assert.IsTrue(dispatcher.Serves("kafkas"));
    }

    [TestMethod]
    public void ExtensionListing_WithExtensionsOn_ListsKafkaInBothForms()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("kafka", StringComparison.Ordinal)));
        Assert.Contains("Produces and consumes Kafka records", ExtensionListing.Markdown(_ => null));
    }

    [TestMethod]
    [DataRow("kafka")]
    [DataRow("kafkas")]
    public async Task Dispatch_WithCurlExtensions0_FailsAsCurlFailsAnUnsupportedScheme(string scheme)
    {
        Assert.IsNull(ExtensionProtocolHandlers.For(name => name == "CURL_EXTENSIONS" ? "0" : null));
        ProtocolDispatcher dispatcher = new([]);

        TransferResult result = await dispatcher.DispatchAsync(new TransferContext { Url = CurlUrl.Parse($"{scheme}://example.com/q"), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual($"Protocol \"{scheme}\" not supported", result.ErrorMessage);
    }
}
