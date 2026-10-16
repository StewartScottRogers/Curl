using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Libcurl.Global;
using Libcurl.UnitTests.Global;
using Libcurl.Url;

namespace Libcurl.UnitTests.Url;

[TestClass]
public sealed class CurlUrlHandleTests
{
    [TestMethod]
    public async Task UrlInit_WrapsANewEngineUrlHandle()
    {
        var engine = new FakeCurlEngine();
        await using var library = CurlGlobal.InitCounting(engine, CurlGlobalFlags.Default, new GlobalInitCounter());

        using var url = library.UrlInit();

        Assert.AreSame(engine.Created.Single(), url.EngineHandle);
        Assert.IsFalse(url.IsCleanedUp);
    }

    [TestMethod]
    public async Task UrlInit_AfterGlobalCleanup_ThrowsObjectDisposed()
    {
        var library = CurlGlobal.InitCounting(new FakeCurlEngine(), CurlGlobalFlags.Default, new GlobalInitCounter());
        await library.DisposeAsync();

        Assert.ThrowsExactly<ObjectDisposedException>(() => library.UrlInit());
    }

    [TestMethod]
    public void Get_PassesThePartAndFlagsAndAnswersTheEnginesValueAndCode()
    {
        var engine = new FakeCurlUrlHandle { GetValue = "example.com" };
        using var url = new CurlUrlHandle(engine);

        var code = url.Get(CurlUrlPart.Host, CurlUrlFlags.UrlDecode | CurlUrlFlags.Punycode, out var value);

        Assert.AreEqual(CurlUrlCode.Ok, code);
        Assert.AreEqual("example.com", value);
        Assert.AreEqual((CurlUrlPart.Host, CurlUrlFlags.UrlDecode | CurlUrlFlags.Punycode), engine.Gets.Single());
    }

    [TestMethod]
    public void Get_TheEngineHasNoSuchPart_AnswersItsCodeAndNull()
    {
        using var url = new CurlUrlHandle(new FakeCurlUrlHandle { Result = CurlUrlCode.NoQuery });

        Assert.AreEqual(CurlUrlCode.NoQuery, url.Get(CurlUrlPart.Query, CurlUrlFlags.None, out var value));
        Assert.IsNull(value);
    }

    [TestMethod]
    public void Set_PassesThePartValueAndFlagsAndAnswersTheEnginesCode()
    {
        var engine = new FakeCurlUrlHandle();
        using var url = new CurlUrlHandle(engine);

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Url, "https://example.com/a", CurlUrlFlags.NonSupportScheme));

        Assert.AreEqual((CurlUrlPart.Url, (string?)"https://example.com/a", CurlUrlFlags.NonSupportScheme), engine.Sets.Single());
    }

    [TestMethod]
    public void Set_ANullValue_ClearsThePartThroughTheEngine()
    {
        var engine = new FakeCurlUrlHandle();
        using var url = new CurlUrlHandle(engine);

        url.Set(CurlUrlPart.Fragment, null, CurlUrlFlags.None);

        Assert.AreEqual((CurlUrlPart.Fragment, (string?)null, CurlUrlFlags.None), engine.Sets.Single());
    }

    [TestMethod]
    public void Set_TheEngineRefuses_AnswersItsCode()
    {
        using var url = new CurlUrlHandle(new FakeCurlUrlHandle { Result = CurlUrlCode.BadPortNumber });

        Assert.AreEqual(CurlUrlCode.BadPortNumber, url.Set(CurlUrlPart.Port, "99999", CurlUrlFlags.None));
    }

    [TestMethod]
    public void Duplicate_DisposingTheCopy_LeavesTheOriginalLive()
    {
        var engine = new FakeCurlUrlHandle();
        using var url = new CurlUrlHandle(engine);

        var copy = url.Duplicate();
        copy.Dispose();

        Assert.AreNotSame(url, copy);
        Assert.AreEqual(1, engine.Duplicated!.DisposeCount);
        Assert.AreEqual(0, engine.DisposeCount);
        Assert.IsFalse(url.IsCleanedUp);
    }

    [TestMethod]
    public void Duplicate_TheCopyHoldsTheEnginesCopy()
    {
        var engine = new FakeCurlUrlHandle();
        using var url = new CurlUrlHandle(engine);

        using var copy = url.Duplicate();

        Assert.AreSame(engine.Duplicated, copy.EngineHandle);
    }

    [TestMethod]
    public void Dispose_DisposesTheEngineHandleOnce()
    {
        var engine = new FakeCurlUrlHandle();
        var url = new CurlUrlHandle(engine);

        url.Dispose();
        url.Dispose();

        Assert.AreEqual(1, engine.DisposeCount);
        Assert.IsTrue(url.IsCleanedUp);
    }

    [TestMethod]
    public void Members_AfterCleanup_ThrowObjectDisposed()
    {
        var url = new CurlUrlHandle(new FakeCurlUrlHandle());
        url.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => url.Get(CurlUrlPart.Url, CurlUrlFlags.None, out _));
        Assert.ThrowsExactly<ObjectDisposedException>(() => url.Set(CurlUrlPart.Url, "https://example.com/", CurlUrlFlags.None));
        Assert.ThrowsExactly<ObjectDisposedException>(() => url.Duplicate());
        Assert.ThrowsExactly<ObjectDisposedException>(() => url.EngineHandle);
    }

    [TestMethod]
    [DataRow(CurlUrlCode.Ok, "No error")]
    [DataRow(CurlUrlCode.BadPortNumber, "Port number was not a decimal number between 0 and 65535")]
    [DataRow(CurlUrlCode.NoQuery, "No query present")]
    [DataRow((CurlUrlCode)999, "CURLUcode unknown")]
    public void ErrorText_AnswersCurlUrlStrerrorsText(CurlUrlCode code, string expected)
    {
        Assert.AreEqual(expected, CurlUrlHandle.ErrorText(code));
    }
}
