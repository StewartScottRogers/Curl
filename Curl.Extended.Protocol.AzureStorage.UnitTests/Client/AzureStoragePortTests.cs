using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.AzureStorage.Client;

/// <summary>Checks that an Azure Storage URL's port is the port connected to and named in <c>Host</c> (BL-5342).</summary>
[TestClass]
public sealed class AzureStoragePortTests
{
    [TestMethod]
    [DataRow("az://acct.blob.core.windows.net:8443/c/x", "acct.blob.core.windows.net", 8443, "acct.blob.core.windows.net:8443")]
    [DataRow("az://127.0.0.1:10000/c/x", "127.0.0.1", 10000, "127.0.0.1:10000")]
    [DataRow("abfss://fs@acct.dfs.core.windows.net:10000/c/x", "acct.dfs.core.windows.net", 10000, "acct.dfs.core.windows.net:10000")]
    [DataRow("az://acct.blob.core.windows.net/c/x", "acct.blob.core.windows.net", 443, "acct.blob.core.windows.net")]
    [DataRow("az://acct.blob.core.windows.net:443/c/x", "acct.blob.core.windows.net", 443, "acct.blob.core.windows.net")]
    public async Task ExecuteAsync_UrlPort_ConnectsToItAndNamesItInHost(string url, string host, int port, string hostHeader)
    {
        QueuedAzureStorageConnector connector = new(QueuedAzureStorageConnector.Reply("data"));
        TransferContext context = new() { Url = CurlUrl.Parse(url), Output = new MemoryStream() };

        TransferResult result = await new AzureStorageProtocolHandler(connector, _ => null, 8, 8).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual((host, port), (connector.Targets[0].Host, connector.Targets[0].Port));
        StringAssert.Contains(connector.Connections[0].Written, $"\r\nHost: {hostHeader}\r\n");
    }
}
