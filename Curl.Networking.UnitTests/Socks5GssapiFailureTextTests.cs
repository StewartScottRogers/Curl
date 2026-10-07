using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

[TestClass]
public sealed class Socks5GssapiFailureTextTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(SecurityContextStatus.NoCredentials, "SEC_E_TARGET_UNKNOWN (0x80090303) - The specified target is unknown or unreachable")]
    [DataRow(SecurityContextStatus.Refused, "SEC_E_TARGET_UNKNOWN (0x80090303) - The specified target is unknown or unreachable")]
    [DataRow(SecurityContextStatus.NoMechanism, "SEC_E_SECPKG_NOT_FOUND (0x80090305) - The requested security package does not exist")]
    [DataRow(SecurityContextStatus.MalformedToken, "SEC_E_INVALID_TOKEN (0x80090308) - The token supplied to the function is invalid")]
    public void ContextFailed_InTheSspiBuild_NamesTheSecurityStatus(SecurityContextStatus status, string text)
    {
        var texts = new Socks5GssapiFailureText(usesSspi: true, "unused");

        Diagnostics.Arrange("uses SSPI, status", $"True, {status}");

        var message = texts.ContextFailed(status);

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", "SSPI error: InitializeSecurityContext failed: " + text, message);

        Assert.AreEqual("SSPI error: InitializeSecurityContext failed: " + text, message);
    }

    [TestMethod]
    [DataRow(SecurityContextStatus.NoCredentials, "No credentials were supplied, or the credentials were unavailable or inaccessible.\nNo Kerberos credentials available (default cache: API:x)")]
    [DataRow(SecurityContextStatus.Refused, "Unspecified GSS failure.  Minor code may provide more information.")]
    [DataRow(SecurityContextStatus.NoMechanism, "An unsupported mechanism was requested.")]
    [DataRow(SecurityContextStatus.MalformedToken, "Invalid token was supplied.")]
    public void ContextFailed_InTheGssapiBuild_NamesMitsStatus(SecurityContextStatus status, string text)
    {
        var texts = new Socks5GssapiFailureText(usesSspi: false, "API:x");

        Diagnostics.Arrange("uses SSPI, credential cache, status", $"False, API:x, {status}");

        var message = texts.ContextFailed(status);

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", "GSS-API error: gss_init_sec_context failed: " + text, message);

        Assert.AreEqual("GSS-API error: gss_init_sec_context failed: " + text, message);
    }

    [TestMethod]
    public void UnwrapFailed_InTheSspiBuild_IsDecryptMessagesFailure()
    {
        var texts = new Socks5GssapiFailureText(usesSspi: true, "unused");

        Diagnostics.Arrange("uses SSPI", true);

        var message = texts.UnwrapFailed;

        Diagnostics.Act("message", message);
        Diagnostics.Assert(
            "message",
            "SSPI error: DecryptMessage failed: SEC_E_MESSAGE_ALTERED (0x8009030f) - The message or signature supplied for verification has been altered",
            message);

        Assert.AreEqual(
            "SSPI error: DecryptMessage failed: SEC_E_MESSAGE_ALTERED (0x8009030f) - The message or signature supplied for verification has been altered",
            message);
    }
}
