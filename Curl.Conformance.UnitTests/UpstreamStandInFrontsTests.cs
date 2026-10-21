namespace Curl.Conformance;

/// <summary>Pins <see cref="UpstreamStandInFronts"/>, the runner's registration table of stand-in fronts.</summary>
[TestClass]
public sealed class UpstreamStandInFrontsTests
{
    [TestMethod]
    public void AddVariables_NoCertificateDirectory_GivesNoTlsPortAValue()
    {
        Dictionary<string, string> variables = [];

        UpstreamStandInFronts.Create(null, null, null).AddVariables(variables, certificateDirectory: null);

        Assert.IsEmpty(variables);
    }

    [TestMethod]
    public void AddVariables_CertificateDirectory_GivesEveryTlsPortItsValue()
    {
        Dictionary<string, string> variables = [];

        UpstreamStandInFronts.Create(null, null, null).AddVariables(variables, certificateDirectory: "/certs");

        CollectionAssert.AreEquivalent(
            new[] { "HTTPSPORT", "FTPSPORT", "SMTPSPORT", "IMAPSPORT", "POP3SPORT", "MQTTSPORT", "GOPHERSPORT", "HTTP2TLSPORT", "HTTPSPROXYPORT" },
            variables.Keys.ToArray());
        Assert.AreEqual(UpstreamCaseRunner.Http2TlsPort, variables["HTTP2TLSPORT"]);
    }

    [TestMethod]
    public void UploadedBytes_NoSshServer_IsEmpty()
    {
        Assert.IsEmpty(UpstreamStandInFronts.Create(null, null, null).UploadedBytes("/nowhere", 1).ToArray());
    }
}
