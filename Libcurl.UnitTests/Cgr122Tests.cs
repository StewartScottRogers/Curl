using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Libcurl.Options;

namespace Libcurl.UnitTests;

/// <summary>
/// CGR-122: <c>CURLOPT_NOBODY</c> set to 1 after <c>CURLOPT_UPLOAD</c> makes the request a <c>HEAD</c> with no body, as
/// libcurl's <c>setopt.c</c> sets its one request method to <c>HTTPREQ_HEAD</c>, replacing the upload's <c>PUT</c>.
/// </summary>
[TestClass]
public sealed class Cgr122Tests
{
    [TestMethod]
    [DataRow(1L)]
    [DataRow(-2L)]
    public void SetOption_NobodyOnAfterUpload_ClearsTheUploadAndSetsNoBody(long value)
    {
        var options = new EasyOptions { Transfer = new EasyTransferOptions { Upload = true, InFileSize = 15 } };

        Assert.AreEqual(CurlCode.Ok, EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Nobody, value));

        Assert.IsFalse(options.Transfer.Upload);
        Assert.IsTrue(options.Transfer.NoBody);
    }

    [TestMethod]
    public void SetOption_NobodyZeroAfterUpload_KeepsTheUpload()
    {
        var options = new EasyOptions { Transfer = new EasyTransferOptions { Upload = true, InFileSize = 15 } };

        Assert.AreEqual(CurlCode.Ok, EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Nobody, 0L));

        Assert.IsTrue(options.Transfer.Upload);
        Assert.IsFalse(options.Transfer.NoBody);
    }
}
