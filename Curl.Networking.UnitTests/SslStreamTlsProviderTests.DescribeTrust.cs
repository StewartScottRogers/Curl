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
        Diagnostics.Arrange("target host", targetHost);

        var trust = SslStreamTlsProvider.DescribeTrust(new TlsClientOptions(), targetHost);

        Diagnostics.Act("targets IP address", trust.TargetsIpAddress);
        Diagnostics.Assert("targets IP address", expected, trust.TargetsIpAddress);
        Assert.AreEqual(expected, trust.TargetsIpAddress);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void DescribeTrust_AutoClientCertificate_IsCarried(bool autoClientCertificate)
    {
        Diagnostics.Arrange("auto client cert", autoClientCertificate);

        var trust = SslStreamTlsProvider.DescribeTrust(new TlsClientOptions(AutoClientCertificate: autoClientCertificate), "localhost");

        Diagnostics.Act("uses automatic client certificate", trust.UsesAutomaticClientCertificate);
        Diagnostics.Assert("uses automatic client certificate", autoClientCertificate, trust.UsesAutomaticClientCertificate);
        Assert.AreEqual(autoClientCertificate, trust.UsesAutomaticClientCertificate);
    }
}
