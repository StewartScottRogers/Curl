using System.Net.Security;

namespace Curl.Networking;

/// <summary>
/// <see cref="OpenSslCipherString" />: the <c>SHA256</c> and <c>SHA384</c> aliases select
/// only the non-GCM suites whose name ends in that hash (AF-0976).
/// </summary>
[TestClass]
public sealed class OpenSslCipherStringHashAliasTests
{
    [TestMethod]
    public void Expand_Sha256Alias_SelectsOnlyNonGcmSuitesEndingSha256()
    {
        var suites = OpenSslCipherString.Expand("SHA256")!.ToList();

        CollectionAssert.Contains(suites, TlsCipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA256);
        CollectionAssert.DoesNotContain(suites, TlsCipherSuite.TLS_RSA_WITH_AES_128_GCM_SHA256);
        CollectionAssert.DoesNotContain(suites, TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA384);
    }

    [TestMethod]
    public void Expand_Sha384Alias_SelectsOnlyNonGcmSuitesEndingSha384()
    {
        var suites = OpenSslCipherString.Expand("SHA384")!.ToList();

        CollectionAssert.Contains(suites, TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA384);
        CollectionAssert.DoesNotContain(suites, TlsCipherSuite.TLS_RSA_WITH_AES_256_GCM_SHA384);
        CollectionAssert.DoesNotContain(suites, TlsCipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA256);
    }
}
