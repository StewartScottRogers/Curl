namespace Curl.Kerberos;

/// <summary>Checks that <see cref="KerberosKdcException" /> names each KRB-ERROR code (RFC 4120 section 7.5.9) it knows and keeps the KDC's code and text.</summary>
[TestClass]
public sealed class KerberosKdcExceptionTests
{
    [TestMethod]
    [DataRow(6, KerberosKdcError.ClientPrincipalUnknown)]
    [DataRow(7, KerberosKdcError.ServerPrincipalUnknown)]
    [DataRow(14, KerberosKdcError.EncryptionTypeNotSupported)]
    [DataRow(18, KerberosKdcError.ClientRevoked)]
    [DataRow(23, KerberosKdcError.PasswordExpired)]
    [DataRow(24, KerberosKdcError.PreAuthenticationFailed)]
    [DataRow(25, KerberosKdcError.PreAuthenticationRequired)]
    [DataRow(32, KerberosKdcError.TicketExpired)]
    [DataRow(37, KerberosKdcError.ClockSkew)]
    [DataRow(60, KerberosKdcError.KdcRefused)]
    public void FromErrorMessage_ErrorCode_MapsToItsFailure(int code, KerberosKdcError expected)
    {
        KerberosErrorMessage message = KerberosErrorMessage.Decode(FakeKdc.Error(code));

        KerberosKdcException failure = KerberosKdcException.FromErrorMessage(message);

        Assert.AreEqual(expected, failure.Error);
        Assert.AreEqual(code, failure.KdcErrorCode);
        Assert.AreEqual($"error {code}", failure.KdcErrorText);
        Assert.AreEqual($"Kerberos ticket could not be got: {expected}.", failure.Message);
    }
}
