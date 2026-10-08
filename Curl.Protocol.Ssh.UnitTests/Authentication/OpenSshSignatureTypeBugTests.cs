using Curl.Testing;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// Pins libssh2 1.11.1's <c>is_version_less_than_78</c> reading of the server's banner
/// (ADR-0311): the text after the first <c>OpenSSH_</c>, read by <c>strtol</c> up to a dot.
/// </summary>
[TestClass]
public sealed class OpenSshSignatureTypeBugTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("SSH-2.0-OpenSSH_7.7", DisplayName = "7.7")]
    [DataRow("SSH-2.0-OpenSSH_7.0p1 Debian", DisplayName = "7.0")]
    [DataRow("SSH-2.0-OpenSSH_6.9", DisplayName = "6.9")]
    [DataRow("SSH-2.0-OpenSSH_1.2", DisplayName = "1.2")]
    [DataRow("SSH-2.0-OpenSSH_7.70", DisplayName = "only the minor's first digit counts")]
    [DataRow("SSH-2.0-OpenSSH_ \t+7.4", DisplayName = "strtol's white space and plus sign")]
    [DataRow("SSH-2.0-Foo OpenSSH_5.3", DisplayName = "OpenSSH_ anywhere in the banner")]
    [DataRow("SSH-2.0-OpenSSH_6.", DisplayName = "a major below 7 needs only the dot")]
    public void AffectsServer_OpenSshBefore78_IsTrue(string identification) =>
        Assert.IsTrue(AffectsServer(identification, expected: true));

    [TestMethod]
    [DataRow(null, DisplayName = "no banner")]
    [DataRow("SSH-2.0-dropbear_2022.83", DisplayName = "not OpenSSH")]
    [DataRow("SSH-2.0-OpenSSH_7.8", DisplayName = "7.8")]
    [DataRow("SSH-2.0-OpenSSH_9.7", DisplayName = "9.7")]
    [DataRow("SSH-2.0-OpenSSH_10.0", DisplayName = "10.0")]
    [DataRow("SSH-2.0-OpenSSH_0.9", DisplayName = "major 0")]
    [DataRow("SSH-2.0-OpenSSH_-7.7", DisplayName = "a negative major")]
    [DataRow("SSH-2.0-OpenSSH_7", DisplayName = "no dot")]
    [DataRow("SSH-2.0-OpenSSH_7p1", DisplayName = "no dot after the major")]
    [DataRow("SSH-2.0-OpenSSH_7.", DisplayName = "7 with the dot last")]
    [DataRow("SSH-2.0-OpenSSH_7.x", DisplayName = "7 with no minor digit")]
    [DataRow("SSH-2.0-OpenSSH_.5", DisplayName = "no major digits")]
    [DataRow("SSH-2.0-OpenSSH_", DisplayName = "nothing after the prefix")]
    [DataRow("SSH-2.0-OpenSSH_99999999999999999999.1", DisplayName = "a major that overflows")]
    public void AffectsServer_AnythingElse_IsFalse(string? identification) =>
        Assert.IsFalse(AffectsServer(identification, expected: false));

    // Reads the banner, writing it, the answer and what the test expects.
    private bool AffectsServer(string? identification, bool expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("server identification", identification is null ? "(none)" : SshAuthenticationDiagnostics.Text(identification));
        bool affected = OpenSshSignatureTypeBug.AffectsServer(identification);
        diagnostics.Act("affects server", affected);
        diagnostics.Assert("affects server", expected, affected);
        return affected;
    }
}
