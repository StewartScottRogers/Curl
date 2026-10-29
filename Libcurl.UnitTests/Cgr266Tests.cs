using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;

namespace Libcurl.UnitTests;

/// <summary>
/// CGR-266: <c>CURLOPT_CONV_FROM_NETWORK_FUNCTION</c> (20142), <c>CONV_TO_NETWORK_FUNCTION</c> (20143) and
/// <c>CONV_FROM_UTF8_FUNCTION</c> (20144) answer <see cref="CurlCode.UnknownOption"/>, as libcurl 8.22.0 does.
/// </summary>
[TestClass]
public sealed class Cgr266Tests
{
    [TestMethod]
    [DataRow(CurlOption.ConvFromNetworkFunction)]
    [DataRow(CurlOption.ConvToNetworkFunction)]
    [DataRow(CurlOption.ConvFromUtf8Function)]
    public void SetOption_ConversionCallback_AnswersUnknownOption(CurlOption option)
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.UnknownOption, EasyOptionDispatch.Default.SetOption(ref options, option, null));
    }
}
