using Curl.Api.Options.Tls;
using Curl.Networking;

namespace Curl.Engine;

/// <summary>
/// CGR-154: curl 8.22.0's Schannel build ignores <c>--doh-cert-status</c>, as it ignores <c>--cert-status</c>
/// (ADR-0795): a DoH server stapling no OCSP response still answers, and the name resolves (measured). The OpenSSL
/// build asks for and checks the stapled status on the DoH handshake.
/// </summary>
[TestClass]
public sealed class Cgr154Tests
{
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void DohOf_DohVerifyStatusOnSchannelBuild_RequiresNoCertificateStatus()
    {
        TlsClientOptions doh = TlsClientOptionsMapping.DohOf(new EasyTlsOptions { DohVerifyStatus = true });

        Assert.IsFalse(doh.RequireCertificateStatus);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void DohOf_DohVerifyStatusOnOpenSslBuild_RequiresCertificateStatus()
    {
        TlsClientOptions doh = TlsClientOptionsMapping.DohOf(new EasyTlsOptions { DohVerifyStatus = true });

        Assert.IsTrue(doh.RequireCertificateStatus);
    }
}
