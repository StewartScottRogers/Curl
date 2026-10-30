using System.Text;

namespace Curl.Extension.ObliviousDoh.Http;

[TestClass]
public sealed class ObliviousDohHttpTests
{
    private static readonly Uri ProxyUri = new("https://proxy.example/proxy?targethost=t.example&targetpath=/dns-query");

    [TestMethod]
    public void RequestHead_Post_CarriesTheMediaTypeOfRfc9230Section4_1() =>
        Assert.AreEqual(
            "POST /proxy?targethost=t.example&targetpath=/dns-query HTTP/1.1\r\nHost: proxy.example\r\nAccept: application/oblivious-dns-message\r\n"
            + "Content-Type: application/oblivious-dns-message\r\nContent-Length: 3\r\nConnection: close\r\n\r\n",
            Encoding.ASCII.GetString(ObliviousDohHttpClient.RequestHead(ProxyUri, [1, 2, 3])));

    [TestMethod]
    public void RequestHead_Get_AsksForAnything() =>
        Assert.AreEqual(
            "GET /.well-known/odohconfigs HTTP/1.1\r\nHost: t.example:8443\r\nAccept: */*\r\nConnection: close\r\n\r\n",
            Encoding.ASCII.GetString(ObliviousDohHttpClient.RequestHead(new Uri("https://t.example:8443/.well-known/odohconfigs"), null)));

    [TestMethod]
    public async Task SendAsync_WritesTheRequestAndReadsTheResponseOverTls()
    {
        var connector = new FakeConnector(_ => FakeConnector.Ok([9, 8]));

        ObliviousDohHttpResponse response = (await new ObliviousDohHttpClient(connector).SendAsync(ProxyUri, [1, 2, 3], CancellationToken.None))!;

        Assert.AreEqual(200, response.StatusCode);
        CollectionAssert.AreEqual(new byte[] { 9, 8 }, response.Body);
        Assert.AreEqual(new Curl.Protocol.Abstractions.ConnectTarget("proxy.example", 443, true), connector.Targets.Single());
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, FakeConnector.BodyOf([.. connector.Connections.Single().Written]));
        Assert.IsTrue(connector.Connections.Single().IsDisposed);
    }

    [TestMethod]
    public async Task SendAsync_HttpUrl_ConnectsWithoutTls()
    {
        var connector = new FakeConnector(_ => FakeConnector.Ok([]));

        await new ObliviousDohHttpClient(connector).SendAsync(new Uri("http://proxy.example:8080/"), null, CancellationToken.None);

        Assert.AreEqual(new Curl.Protocol.Abstractions.ConnectTarget("proxy.example", 8080, false), connector.Targets.Single());
    }

    [TestMethod]
    public async Task SendAsync_ConnectFails_GivesNull() =>
        Assert.IsNull(await new ObliviousDohHttpClient(new FakeConnector(_ => []) { Fails = true }).SendAsync(ProxyUri, null, CancellationToken.None));

    [TestMethod]
    public async Task SendAsync_ConnectionResets_GivesNull() =>
        Assert.IsNull(await new ObliviousDohHttpClient(new FakeConnector(_ => []) { ThrowOnWrite = true }).SendAsync(ProxyUri, null, CancellationToken.None));

    [TestMethod]
    public async Task SendAsync_ResponsePastTheLimit_GivesNull() =>
        Assert.IsNull(await new ObliviousDohHttpClient(new FakeConnector(_ => new byte[ObliviousDohHttpClient.MaximumResponseLength + 1])).SendAsync(ProxyUri, null, CancellationToken.None));

    [TestMethod]
    public async Task SendAsync_MalformedResponse_GivesNull() =>
        Assert.IsNull(await new ObliviousDohHttpClient(new FakeConnector(_ => [1, 2])).SendAsync(ProxyUri, null, CancellationToken.None));

    [TestMethod]
    public async Task SendAsync_NullUri_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await new ObliviousDohHttpClient(new FakeConnector(_ => [])).SendAsync(null!, null, CancellationToken.None));

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nabXX", "ab")]
    [DataRow("HTTP/1.1 200 OK\r\nX-Note\r\n\r\nto close", "to close")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n2;ext=1\r\nab\r\n1\r\nc\r\n0\r\n\r\n", "abc")]
    public void Parse_FramesTheBodyAsRfc9112Section6_3(string response, string body) =>
        Assert.AreEqual(body, Encoding.ASCII.GetString(ObliviousDohHttpResponseParser.Parse(Encoding.ASCII.GetBytes(response))!.Body));

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\n")]
    [DataRow("HTTP/1.1\r\n\r\n")]
    [DataRow("HTTP/2 200\r\n\r\n")]
    [DataRow("HTTP/1.1 OK OK\r\n\r\n")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nab")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: x\r\n\r\nab")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\nzz\r\nab\r\n0\r\n\r\n")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nab")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n2\r\nab\r\n")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\nFFFFFFFF\r\nab\r\n0\r\n\r\n")]
    public void Parse_MalformedOrCutShort_GivesNull(string response) =>
        Assert.IsNull(ObliviousDohHttpResponseParser.Parse(Encoding.ASCII.GetBytes(response)));
}
