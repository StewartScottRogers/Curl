namespace Curl.Extended.Protocol.AzureStorage.Requests;

/// <summary>Pins how the URL's port reaches the endpoint and the <c>Host</c> header (BL-5342).</summary>
[TestClass]
public sealed class AzureStoragePortMappingTests
{
    [TestMethod]
    [DataRow("az://127.0.0.1:10000/c/x", 10000)]
    [DataRow("abfs://fs@acct.dfs.core.windows.net:8443/x", 8443)]
    [DataRow("az://acct/c/x", 443)]
    public void Map_UrlPort_IsKeptOrDefaultsTo443(string url, int port) =>
        Assert.AreEqual(port, AzureStorageUrlMapping.Map(new Uri(url)).Port);

    [TestMethod]
    [DataRow(443, "h.example")]
    [DataRow(10000, "h.example:10000")]
    public void HostHeader_Port_IsNamedOnlyWhenNotDefault(int port, string expected) =>
        Assert.AreEqual(expected, new AzureStorageEndpoint("h.example", "/", string.Empty, port).HostHeader);
}
