using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;

namespace Libcurl.UnitTests;

/// <summary>
/// CGR-125: the deprecated <c>CURLOPT_SSL_ENABLE_NPN</c>, <c>CURLOPT_EGDSOCKET</c> and <c>CURLOPT_RANDOM_FILE</c>
/// answer <see cref="CurlCode.Ok"/> for every value and change nothing, as libcurl 8.22.0 does
/// (measured 2026-10-26 against libcurl/8.22.0 Schannel, BL-3654 Notes).
/// </summary>
[TestClass]
public sealed class Cgr125Tests
{
    [TestMethod]
    [DataRow(0L)]
    [DataRow(1L)]
    [DataRow(2L)]
    [DataRow(-1L)]
    public void SetOption_SslEnableNpn_AnswersOkAndChangesNothing(long value)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.Ok, EasyOptionDispatch.Default.SetOption(ref options, CurlOption.SslEnableNpn, value));

        Assert.AreSame(before, options);
    }

    [TestMethod]
    [DataRow(CurlOption.Egdsocket, "/tmp/egd")]
    [DataRow(CurlOption.Egdsocket, "")]
    [DataRow(CurlOption.Egdsocket, null)]
    [DataRow(CurlOption.RandomFile, "/dev/urandom")]
    [DataRow(CurlOption.RandomFile, "")]
    [DataRow(CurlOption.RandomFile, null)]
    public void SetOption_DeprecatedRandomSource_AnswersOkAndChangesNothing(CurlOption option, string? value)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.Ok, EasyOptionDispatch.Default.SetOption(ref options, option, value));

        Assert.AreSame(before, options);
    }
}
