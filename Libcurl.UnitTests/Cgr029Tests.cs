using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Libcurl.Options;

namespace Libcurl.UnitTests;

/// <summary>CGR-029: <c>CURLOPT_PUT</c> is upstream's alias of <c>CURLOPT_UPLOAD</c>, so setting it makes the transfer an upload (a PUT over HTTP).</summary>
[TestClass]
public sealed class Cgr029Tests
{
    [TestMethod]
    [DataRow(1L)]
    [DataRow(40L)]
    [DataRow(-3L)]
    public void SetOption_PutNonZero_AnswersOkAndMakesTheTransferAnUpload(long value)
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.Ok, EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Put, value));
        Assert.IsTrue(options.Transfer.Upload);
    }

    [TestMethod]
    public void SetOption_PutZero_AnswersOkAndClearsAnEarlierUpload()
    {
        var options = new EasyOptions { Transfer = new EasyTransferOptions { Upload = true } };

        Assert.AreEqual(CurlCode.Ok, EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Put, 0L));
        Assert.IsFalse(options.Transfer.Upload);
    }
}
