using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Mime;
using Libcurl.Options;

namespace Libcurl.UnitTests.Mime.Mime;

[TestClass]
public sealed class EasyMimePostSetterTests
{
    [TestMethod]
    public void SetOption_Mimepost_SetsTheHandlesBodyAsItIsNow()
    {
        var builder = new FakeMimeBuilder();
        using var mime = new CurlMime(builder);
        mime.AddPart().SetName("a");
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.Ok, EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Mimepost, mime));
        mime.AddPart().SetName("b");

        Assert.AreEqual("a", options.Http.MimePost!.Parts.Single().Name);
    }

    [TestMethod]
    public void SetOption_MimepostNull_ClearsTheBody()
    {
        using var mime = new CurlMime(new FakeMimeBuilder());
        var options = new EasyOptions();
        EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Mimepost, mime);

        Assert.AreEqual(CurlCode.Ok, EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Mimepost, null));

        Assert.IsNull(options.Http.MimePost);
    }

    [TestMethod]
    public void SetOption_MimepostNull_LeavesAnEmptyFormSoTheHandlePostsZeroBytes()
    {
        var options = new EasyOptions { Transfer = new EasyOptions().Transfer with { NoBody = true } };

        Assert.AreEqual(CurlCode.Ok, EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Mimepost, null));

        Assert.IsEmpty(options.Http.HttpPost!.Parts);
        Assert.IsFalse(options.Transfer.NoBody);
    }

    [TestMethod]
    public void SetOption_MimepostAHandle_ClearsNoBody()
    {
        using var mime = new CurlMime(new FakeMimeBuilder());
        var options = new EasyOptions { Transfer = new EasyOptions().Transfer with { NoBody = true } };

        Assert.AreEqual(CurlCode.Ok, EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Mimepost, mime));

        Assert.IsFalse(options.Transfer.NoBody);
    }

    [TestMethod]
    public void SetOption_MimepostNotAMimeHandle_AnswersBadFunctionArgumentAndChangesNothing()
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.BadFunctionArgument, EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Mimepost, "form"));

        Assert.AreEqual(new EasyOptions().Http, options.Http);
    }

    [TestMethod]
    public void SetOption_MimepostAFreedHandle_ThrowsObjectDisposed()
    {
        var mime = new CurlMime(new FakeMimeBuilder());
        mime.Dispose();
        var options = new EasyOptions();

        Assert.ThrowsExactly<ObjectDisposedException>(() => EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Mimepost, mime));
    }
}
