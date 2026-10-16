namespace Curl.Api.Info;

[TestClass]
public sealed class TlsBuildAnswersTests
{
    [TestMethod]
    public void For_Windows_AnswersTheSchannelBuild()
    {
        Assert.AreEqual(new TlsBuildAnswers(8, null, null), TlsBuildAnswers.For(windows: true, macOS: false));
    }

    [TestMethod]
    public void For_MacOS_AnswersTheOpenSslBuildWithEtcSslCertPemAndNoPath()
    {
        Assert.AreEqual(new TlsBuildAnswers(1, "/etc/ssl/cert.pem", null), TlsBuildAnswers.For(windows: false, macOS: true));
    }

    [TestMethod]
    public void For_Linux_AnswersTheOpenSslBuildWithTheDebianBundleAndEtcSslCerts()
    {
        Assert.AreEqual(
            new TlsBuildAnswers(1, "/etc/ssl/certs/ca-certificates.crt", "/etc/ssl/certs"),
            TlsBuildAnswers.For(windows: false, macOS: false));
    }

    [TestMethod]
    public void Current_IsThePlatformsAnswers()
    {
        Assert.AreEqual(TlsBuildAnswers.For(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()), TlsBuildAnswers.Current);
    }
}
