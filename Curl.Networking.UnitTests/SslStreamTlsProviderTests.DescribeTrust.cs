namespace Curl.Networking;

/// <summary>
/// Pins what <see cref="SslStreamTlsProvider.DescribeTrust" /> tells the Schannel build's
/// <c>schannel:</c> lines (BL-1083): <c>--ssl-auto-client-cert</c> and whether the target is
/// an IP address.
/// </summary>
public sealed partial class SslStreamTlsProviderTests
{
    [TestMethod]
    [DataRow("127.0.0.1", true)]
    [DataRow("[::1]", true)]
    [DataRow("::1", true)]
    [DataRow("localhost", false)]
    [DataRow("example.com", false)]
    public void DescribeTrust_TargetHost_SaysWhetherItIsAnIpAddress(string targetHost, bool expected)
    {
        var trust = SslStreamTlsProvider.DescribeTrust(new TlsClientOptions(), targetHost);

        Assert.AreEqual(expected, trust.TargetsIpAddress);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void DescribeTrust_AutoClientCertificate_IsCarried(bool autoClientCertificate)
    {
        var trust = SslStreamTlsProvider.DescribeTrust(new TlsClientOptions(AutoClientCertificate: autoClientCertificate), "localhost");

        Assert.AreEqual(autoClientCertificate, trust.UsesAutomaticClientCertificate);
    }
}
