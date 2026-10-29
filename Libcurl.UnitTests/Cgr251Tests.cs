using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;

namespace Libcurl.UnitTests;

/// <summary>
/// CGR-251: setting <c>CURLOPT_POSTFIELDSIZE</c> after <c>CURLOPT_COPYPOSTFIELDS</c> discards the copied data,
/// as libcurl 8.22.0 does, instead of sending its first bytes.
/// </summary>
[TestClass]
public sealed class Cgr251Tests
{
    [TestMethod]
    public void SetOption_PostFieldSizeAfterCopyPostFields_DiscardsTheCopy()
    {
        var options = new EasyOptions();
        EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Copypostfields, "abcdef");

        Assert.AreEqual(CurlCode.Ok, EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Postfieldsize, 3L));

        Assert.IsNull(options.Http.PostFields);
        Assert.AreEqual(3L, options.Http.PostFieldSize);
        Assert.IsTrue(options.Http.Post);
    }

    [TestMethod]
    [DataRow(5L, true)]
    [DataRow(10L, true)]
    [DataRow(11L, false)]
    public void SetOption_PostFieldSizeAfterSizedCopyPostFields_DropsTheCopyOnlyWhenTheSizeGrows(long size, bool kept)
    {
        var options = new EasyOptions();
        EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Postfieldsize, 10L);
        EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Copypostfields, "abcdefghij");

        EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Postfieldsize, size);

        Assert.AreEqual(kept, options.Http.PostFields is not null);
        Assert.AreEqual(kept, options.Http.PostFieldsAreCopy);
        Assert.AreEqual(size, options.Http.PostFieldSize);
    }

    [TestMethod]
    public void SetOption_PostFieldSizeAfterPostFields_KeepsTheBuffer()
    {
        var options = new EasyOptions();
        EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Postfields, "abcdef");

        EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Postfieldsize, 3L);

        Assert.AreEqual(6, options.Http.PostFields!.Value.Length);
    }
}
