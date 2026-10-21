using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>Pins <see cref="UpstreamHttpsMtlsFront"/>, the stand-in for upstream's https-mtls server (BL-2859).</summary>
[TestClass]
public sealed class UpstreamHttpsMtlsFrontTests
{
    [TestMethod]
    public void Wrap_CaseNamingTheHttpsMtlsServer_PutsTheMtlsServerInFront()
    {
        UpstreamTestCase testCase = ParsedTestCase.From("<client>\n<server>\nhttps-mtls\n</server>\n</client>\n");
        SwsHttpServerConnector sws = new(testCase);

        IConnector wrapped = new UpstreamHttpsMtlsFront().Wrap(sws, new UpstreamStandInContext(testCase, MtlsTestCertificates.CertificateDirectory, sws));

        Assert.IsInstanceOfType<HttpsServerConnector>(wrapped);
    }

    [TestMethod]
    public void Wrap_CaseNamingAnotherServer_LeavesTheConnectorAsItIs()
    {
        UpstreamTestCase testCase = ParsedTestCase.From("<client>\n<server>\nhttps\n</server>\n</client>\n");
        SwsHttpServerConnector sws = new(testCase);

        IConnector wrapped = new UpstreamHttpsMtlsFront().Wrap(sws, new UpstreamStandInContext(testCase, "/certs", sws));

        Assert.AreSame(sws, wrapped);
    }
}
