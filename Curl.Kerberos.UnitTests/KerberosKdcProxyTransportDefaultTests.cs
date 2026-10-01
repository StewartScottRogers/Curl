namespace Curl.Kerberos;

/// <summary>
/// Pins the default anchored overload of <see cref="IKerberosKdcProxyTransport.PostAsync(string, int, string, IReadOnlyList{string}, ReadOnlyMemory{byte}, CancellationToken)" />:
/// with no anchors it is the five-argument exchange; with any it fails with an
/// <see cref="IOException" />, never falling back to the system's trust store (ADR-0300).
/// </summary>
[TestClass]
public sealed class KerberosKdcProxyTransportDefaultTests
{
    [TestMethod]
    public async Task PostAsync_NoAnchors_PostsAsTheFiveArgumentOverload()
    {
        IKerberosKdcProxyTransport transport = new EchoingTransport();

        byte[] reply = await transport.PostAsync("proxy", 443, "KdcProxy", [], new byte[] { 0x30 }, CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 0x30 }, reply);
    }

    [TestMethod]
    public async Task PostAsync_AnAnchor_FailsWithIOException()
    {
        IKerberosKdcProxyTransport transport = new EchoingTransport();

        IOException failure = await Assert.ThrowsExactlyAsync<IOException>(
            () => transport.PostAsync("proxy", 443, "KdcProxy", ["FILE:/ca.pem"], new byte[] { 0x30 }, CancellationToken.None));

        Assert.AreEqual("The KDC proxy proxy port 443 cannot be verified against http_anchors by this transport.", failure.Message);
    }

    private sealed class EchoingTransport : IKerberosKdcProxyTransport
    {
        public Task<byte[]> PostAsync(string host, int port, string path, ReadOnlyMemory<byte> body, CancellationToken cancellationToken) =>
            Task.FromResult(body.ToArray());
    }
}
