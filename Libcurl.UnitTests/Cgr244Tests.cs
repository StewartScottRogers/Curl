using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;

namespace Libcurl.UnitTests;

/// <summary>
/// CGR-244: <c>curl_easy_setopt</c> answers <see cref="CurlCode.BadFunctionArgument"/> (43) for the out-of-range
/// values libcurl 8.22.0 refuses, instead of clamping or storing them.
/// </summary>
[TestClass]
public sealed class Cgr244Tests
{
    [TestMethod]
    [DataRow(CurlOption.Buffersize, -1L)]
    [DataRow(CurlOption.UploadBuffersize, -1L)]
    [DataRow(CurlOption.TftpBlksize, -1L)]
    [DataRow(CurlOption.RtspClientCseq, -1L)]
    [DataRow(CurlOption.RtspServerCseq, -1L)]
    [DataRow(CurlOption.RtspRequest, 0L)]
    [DataRow(CurlOption.RtspRequest, 12L)]
    [DataRow(CurlOption.AltsvcCtrl, 0L)]
    public void SetOption_OutOfRangeNumber_AnswersBadFunctionArgument(CurlOption option, long value)
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.BadFunctionArgument, EasyOptionDispatch.Default.SetOption(ref options, option, value));
    }

    [TestMethod]
    [DataRow("if!")]
    [DataRow("host!")]
    [DataRow("ifhost!")]
    public void SetOption_InterfaceWithEmptyName_AnswersBadFunctionArgument(string value)
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.BadFunctionArgument, EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Interface, value));
    }

    [TestMethod]
    public void SetOption_InRangeValues_AnswerOk()
    {
        var options = new EasyOptions();
        var dispatch = EasyOptionDispatch.Default;

        Assert.AreEqual(CurlCode.Ok, dispatch.SetOption(ref options, CurlOption.Buffersize, 0L));
        Assert.AreEqual(CurlCode.Ok, dispatch.SetOption(ref options, CurlOption.UploadBuffersize, 0L));
        Assert.AreEqual(CurlCode.Ok, dispatch.SetOption(ref options, CurlOption.TftpBlksize, 0L));
        Assert.AreEqual(CurlCode.Ok, dispatch.SetOption(ref options, CurlOption.RtspClientCseq, 0L));
        Assert.AreEqual(CurlCode.Ok, dispatch.SetOption(ref options, CurlOption.RtspRequest, 1L));
        Assert.AreEqual(CurlCode.Ok, dispatch.SetOption(ref options, CurlOption.AltsvcCtrl, 4L));
        Assert.AreEqual(CurlCode.Ok, dispatch.SetOption(ref options, CurlOption.Interface, "if!eth0"));
    }
}
