using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;

namespace Libcurl.UnitTests;

[TestClass]
public sealed class Cgr265Tests
{
    [TestMethod]
    [DataRow(CurlOption.TlsauthType, "SRP")]
    [DataRow(CurlOption.TlsauthType, "PSK")]
    [DataRow(CurlOption.TlsauthUsername, "u")]
    [DataRow(CurlOption.TlsauthPassword, "p")]
    [DataRow(CurlOption.ProxyTlsauthType, "SRP")]
    [DataRow(CurlOption.ProxyTlsauthType, "PSK")]
    [DataRow(CurlOption.ProxyTlsauthUsername, "u")]
    [DataRow(CurlOption.ProxyTlsauthPassword, "p")]
    public void SetOption_TlsAuthOptionWithoutSrp_IsNotBuiltIn(CurlOption option, object value)
    {
        var was = EasyAuthenticationOptionSetters.SrpIsBuiltIn;
        EasyAuthenticationOptionSetters.SrpIsBuiltIn = false;
        try
        {
            var options = new EasyOptions();
            var before = options;

            Assert.AreEqual(CurlCode.NotBuiltIn, EasyOptionDispatch.Default.SetOption(ref options, option, value));
            Assert.AreSame(before, options);
        }
        finally
        {
            EasyAuthenticationOptionSetters.SrpIsBuiltIn = was;
        }
    }
}
