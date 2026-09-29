namespace Curl.Kerberos;

/// <summary>Checks that a <see cref="KerberosFileException" /> carries a KCM daemon's status only when one refused.</summary>
[TestClass]
public sealed class KerberosFileExceptionTests
{
    [TestMethod]
    public void Constructor_KcmStatus_IsKeptAndNamedInTheMessage()
    {
        KerberosFileException failure = new(KerberosFileError.KcmFailed, -1765328243);

        Assert.AreEqual(KerberosFileError.KcmFailed, failure.Error);
        Assert.AreEqual(-1765328243, failure.KcmStatus);
        Assert.AreEqual("Kerberos file could not be read: KcmFailed (KCM status -1765328243).", failure.Message);
    }

    [TestMethod]
    public void Constructor_NoKcmStatus_HasNone() =>
        Assert.IsNull(new KerberosFileException(KerberosFileError.NotFound).KcmStatus);
}
