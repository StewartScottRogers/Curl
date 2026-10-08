using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// <see cref="ClientCertificateArgument" />: each case is a <c>--cert</c> value measured
/// 2026-09-26 against curl 8.21.0's Schannel build on Windows, which names the file it
/// split off in its exit 58 message, and against curl 8.18.0's OpenSSL build on Linux.
/// </summary>
[TestClass]
public sealed class ClientCertificateArgumentTests
{
    private const bool WindowsBuild = true;

    private const bool OpenSslBuild = false;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("nonexist.p12", "nonexist.p12", null)]
    [DataRow("nonexist.p12:pw", "nonexist.p12", "pw")]
    [DataRow("colon.p12:sec:ret", "colon.p12", "sec:ret")]
    [DataRow("nonexist.p12:", "nonexist.p12", null)]
    [DataRow(":pw", "", "pw")]
    [DataRow(@"nonexist.p12\:x:pw", "nonexist.p12:x", "pw")]
    [DataRow(@"nonexist\\x.p12", @"nonexist\x.p12", null)]
    [DataRow(@"nonexist\x.p12", @"nonexist\x.p12", null)]
    [DataRow(@"nonexist.p12\", @"nonexist.p12\", null)]
    [DataRow("pkcs11:foo", "pkcs11:foo", null)]
    [DataRow("PKCS11:foo", "PKCS11:foo", null)]
    public void Split_OnEitherBuild_SplitsAsCurlDoes(string value, string expectedPath, string? expectedPassphrase)
    {
        Diagnostics.Arrange("value", value);
        Diagnostics.Arrange("expected path", expectedPath);
        Diagnostics.Arrange("expected passphrase", expectedPassphrase);
        foreach (var recognisesDriveLetters in new[] { WindowsBuild, OpenSslBuild })
        {
            Diagnostics.Arrange("recognises drive letters", recognisesDriveLetters);
            var (path, passphrase) = ClientCertificateArgument.Split(value, recognisesDriveLetters);
            Diagnostics.Act("path", path);
            Diagnostics.Act("passphrase", passphrase);
            Diagnostics.Assert("path", expectedPath, path);
            Diagnostics.Assert("passphrase", expectedPassphrase, passphrase);

            Assert.AreEqual(expectedPath, path);
            Assert.AreEqual(expectedPassphrase, passphrase);
        }
    }

    [TestMethod]
    [DataRow(@"C:\certs\client.p12:secret", @"C:\certs\client.p12", "secret")]
    [DataRow(@"c:\nonexist.p12", @"c:\nonexist.p12", null)]
    [DataRow("C:/nonexist.p12:pw", "C:/nonexist.p12", "pw")]
    [DataRow(@"C:\\nonexist.p12:x", @"C:\nonexist.p12", "x")]
    [DataRow(@"C:\:x", "C::x", null)]
    [DataRow("C:nonexist.p12:pw", "C", "nonexist.p12:pw")]
    [DataRow(@"1:\nonexist.p12:pw", "1", @"\nonexist.p12:pw")]
    [DataRow("C:", "C", null)]
    public void Split_OnTheWindowsBuild_KeepsADriveLettersColon(string value, string expectedPath, string? expectedPassphrase)
    {
        Diagnostics.Arrange("value", value);
        Diagnostics.Arrange("expected path", expectedPath);
        Diagnostics.Arrange("expected passphrase", expectedPassphrase);
        var (path, passphrase) = ClientCertificateArgument.Split(value, WindowsBuild);
        Diagnostics.Act("path", path);
        Diagnostics.Act("passphrase", passphrase);
        Diagnostics.Assert("path", expectedPath, path);
        Diagnostics.Assert("passphrase", expectedPassphrase, passphrase);

        Assert.AreEqual(expectedPath, path);
        Assert.AreEqual(expectedPassphrase, passphrase);
    }

    [TestMethod]
    public void Split_OnTheOpenSslBuild_SplitsAtADriveLettersColon()
    {
        const string value = @"C:\nonexist.pem:pw";
        Diagnostics.Arrange("value", value);
        var (path, passphrase) = ClientCertificateArgument.Split(value, OpenSslBuild);
        Diagnostics.Act("path", path);
        Diagnostics.Act("passphrase", passphrase);
        Diagnostics.Assert("path", "C", path);
        Diagnostics.Assert("passphrase", @"\nonexist.pem:pw", passphrase);

        Assert.AreEqual("C", path);
        Assert.AreEqual(@"\nonexist.pem:pw", passphrase);
    }
}
