using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;

namespace Libcurl.UnitTests;

/// <summary>
/// CGR-245: <c>curl_easy_setopt</c> answers <see cref="CurlCode.Ok"/> for the values libcurl 8.22.0 accepts:
/// any <c>PROXY_TRANSFER_MODE</c> (a boolean), <c>MAXCONNECTS</c> 0 and a negative <c>ADDRESS_SCOPE</c>.
/// </summary>
[TestClass]
public sealed class Cgr245Tests
{
    [TestMethod]
    [DataRow(CurlOption.ProxyTransferMode, 2L)]
    [DataRow(CurlOption.ProxyTransferMode, -1L)]
    [DataRow(CurlOption.Maxconnects, 0L)]
    [DataRow(CurlOption.AddressScope, -1L)]
    [DataRow(CurlOption.AddressScope, -2L)]
    [DataRow(CurlOption.AddressScope, -3L)]
    [DataRow(CurlOption.AddressScope, (long)int.MinValue)]
    public void SetOption_ValueLibcurlAccepts_AnswersOk(CurlOption option, long value)
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.Ok, EasyOptionDispatch.Default.SetOption(ref options, option, value));
    }

    [TestMethod]
    public void SetOption_ProxyTransferModeTwo_TurnsTheModeOn()
    {
        var options = new EasyOptions();

        EasyOptionDispatch.Default.SetOption(ref options, CurlOption.ProxyTransferMode, 2L);

        Assert.IsTrue(options.Transfer.ProxyTransferMode);
    }

    [TestMethod]
    public void SetOption_NegativeAddressScope_StoresItAsUnsigned()
    {
        var options = new EasyOptions();

        EasyOptionDispatch.Default.SetOption(ref options, CurlOption.AddressScope, -1L);

        Assert.AreEqual((long)uint.MaxValue, options.Network.AddressScope);
    }
}
