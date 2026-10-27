using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;

namespace Libcurl.UnitTests;

/// <summary>
/// CGR-124: <c>CURLOPT_UPLOAD_FLAGS</c> (327) answers <see cref="CurlCode.Ok"/> for every value and stores its low byte,
/// as libcurl 8.22.0's <c>setopt.c</c> does (measured 2026-10-26 against libcurl/8.22.0 Schannel, BL-3653 Notes).
/// </summary>
[TestClass]
public sealed class Cgr124Tests
{
    [TestMethod]
    [DataRow(0L, (byte)0)]
    [DataRow(1L, (byte)1)]
    [DataRow(31L, (byte)31)]
    [DataRow(255L, (byte)255)]
    [DataRow(256L, (byte)0)]
    [DataRow(-1L, (byte)255)]
    public void SetOption_UploadFlags_AnswersOkAndStoresTheLowByte(long value, byte stored)
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.Ok, EasyOptionDispatch.Default.SetOption(ref options, CurlOption.UploadFlags, value));

        Assert.AreEqual(stored, options.Protocols.Smtp.UploadFlags);
    }

    [TestMethod]
    public void EasyOptions_UploadFlagsUnset_IsSeen()
    {
        Assert.AreEqual((byte)16, new EasyOptions().Protocols.Smtp.UploadFlags);
    }
}
