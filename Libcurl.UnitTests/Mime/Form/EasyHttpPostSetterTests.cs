using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.Options;
using Libcurl.Mime;
using Libcurl.Options;

namespace Libcurl.UnitTests.Mime.Form;

// CurlForm is obsolete on purpose, as upstream's form API is deprecated; these tests pin it anyway.
#pragma warning disable CS0618

[TestClass]
public sealed class EasyHttpPostSetterTests
{
    [TestMethod]
    public void SetOption_Httppost_SetsTheFormsBodyAsItIsNow()
    {
        CurlForm? form = new CurlForm(new FakeFormBuilder());
        CurlForm.Add(null!, ref form, new CurlFormArgument(CurlFormOption.CopyName, "a"));
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.Ok, EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Httppost, form));
        CurlForm.Add(null!, ref form, new CurlFormArgument(CurlFormOption.CopyName, "b"));

        Assert.AreEqual("a", options.Http.HttpPost!.Parts.Single().Name);
    }

    [TestMethod]
    public void SetOption_HttppostNull_SetsAnEmptyFormThatStillPosts()
    {
        CurlForm? form = new CurlForm(new FakeFormBuilder());
        CurlForm.Add(null!, ref form, new CurlFormArgument(CurlFormOption.CopyName, "a"));
        var options = new EasyOptions();
        EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Httppost, form);

        Assert.AreEqual(CurlCode.Ok, EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Httppost, null));

        Assert.IsEmpty(options.Http.HttpPost!.Parts);
    }

    [TestMethod]
    public void SetOption_Httppost_ClearsNoBody()
    {
        var options = new EasyOptions();
        EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Nobody, 1L);

        Assert.AreEqual(CurlCode.Ok, EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Httppost, null));

        Assert.IsFalse(options.Transfer.NoBody);
    }

    [TestMethod]
    public void SetOption_HttppostNotAForm_AnswersBadFunctionArgumentAndChangesNothing()
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.BadFunctionArgument, EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Httppost, "form"));

        Assert.AreEqual(new EasyOptions().Http, options.Http);
    }

    [TestMethod]
    public void SetOption_HttppostAFreedForm_ThrowsObjectDisposed()
    {
        var form = new CurlForm(new FakeFormBuilder());
        form.Dispose();
        var options = new EasyOptions();

        Assert.ThrowsExactly<ObjectDisposedException>(() => EasyOptionDispatch.Default.SetOption(ref options, CurlOption.Httppost, form));
    }
}
