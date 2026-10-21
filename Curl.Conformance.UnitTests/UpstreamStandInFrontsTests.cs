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

        CollectionAssert.AreEquivalent(new[] { "SMBPORT" }, variables.Keys.ToArray());
    }

    [TestMethod]
    public void AddVariables_GivesSmbPortTheSmbStandInsPort()
    {
        Dictionary<string, string> variables = [];

        UpstreamStandInFronts.Create(null, null, null).AddVariables(variables, certificateDirectory: null);

        Assert.AreEqual(UpstreamCaseRunner.SmbPort, variables["SMBPORT"]);
        Assert.AreEqual(UpstreamCaseRunner.SmbPort, SmbServerConnector.SmbPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void WithFeatures_ReportsTheSmbProtocol()
    {
        UpstreamCurlPlatform platform = UpstreamStandInFronts.Create(null, null, null).WithFeatures(UpstreamCurlPlatform.All[0]);

        Assert.Contains("smb", platform.Features);
    }

    [TestMethod]
    public void AddVariables_CertificateDirectory_GivesEveryTlsPortItsValue()
    {
        Dictionary<string, string> variables = [];

        UpstreamStandInFronts.Create(null, null, null).AddVariables(variables, certificateDirectory: "/certs");

        CollectionAssert.AreEquivalent(
            new[] { "HTTPSPORT", "HTTPS-MTLSPORT", "FTPSPORT", "SMTPSPORT", "IMAPSPORT", "POP3SPORT", "MQTTSPORT", "GOPHERSPORT", "HTTP2TLSPORT", "HTTPSPROXYPORT", "SMBPORT" },
            variables.Keys.ToArray());
        Assert.AreEqual(UpstreamCaseRunner.Http2TlsPort, variables["HTTP2TLSPORT"]);
        Assert.AreEqual("9021", variables["HTTPS-MTLSPORT"]);
    }

    [TestMethod]
    public void UploadedBytes_NoSshServer_IsEmpty()
    {
        Assert.IsEmpty(UpstreamStandInFronts.Create(null, null, null).UploadedBytes("/nowhere", 1).ToArray());
    }
}
