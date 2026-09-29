using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="NegotiateFailureLines" />: the line each platform curl writes when a
/// Negotiate context makes no token, the no-credentials pair as measured (ADR-0176, ADR-0228).
/// </summary>
[TestClass]
public sealed class NegotiateFailureLinesTests
{
    [TestMethod]
    [DataRow(SecurityContextStatus.NoCredentials, "InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package")]
    [DataRow(SecurityContextStatus.NoMechanism, "InitializeSecurityContext failed: SEC_E_SECPKG_NOT_FOUND (0x80090305) - The requested security package does not exist")]
    [DataRow(SecurityContextStatus.Refused, "InitializeSecurityContext failed: SEC_E_LOGON_DENIED (0x8009030c) - The logon attempt failed")]
    [DataRow(SecurityContextStatus.MalformedToken, "InitializeSecurityContext failed: SEC_E_INVALID_TOKEN (0x80090308) - The token supplied to the function is invalid")]
    public void For_SspiWording_NamesTheSspiStatus(SecurityContextStatus status, string expected)
    {
        Assert.AreEqual(expected, NegotiateFailureLines.For(status, wordsAsSspi: true));
    }

    [TestMethod]
    [DataRow(SecurityContextStatus.NoCredentials, "gss_init_sec_context() failed: No credentials were supplied, or the credentials were unavailable or inaccessible. SPNEGO cannot find mechanisms to negotiate. ")]
    [DataRow(SecurityContextStatus.NoMechanism, "gss_init_sec_context() failed: An unsupported mechanism was requested. ")]
    [DataRow(SecurityContextStatus.Refused, "gss_init_sec_context() failed: Unspecified GSS failure.  Minor code may provide more information. ")]
    [DataRow(SecurityContextStatus.MalformedToken, "gss_init_sec_context() failed: Invalid token was supplied. ")]
    public void For_GssApiWording_GivesTheGssApiMessages(SecurityContextStatus status, string expected)
    {
        Assert.AreEqual(expected, NegotiateFailureLines.For(status, wordsAsSspi: false));
    }
}
