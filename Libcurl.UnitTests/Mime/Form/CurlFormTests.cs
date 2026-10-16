using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.UnitTests.Global;

namespace Libcurl.UnitTests.Mime.Form;

// CurlForm is obsolete on purpose, as upstream's form API is deprecated; these tests pin it anyway.
#pragma warning disable CS0618

[TestClass]
public sealed class CurlFormTests
{
    [TestMethod]
    public async Task Add_ANullForm_StartsAFormOverANewEngineBuilder()
    {
        var engine = new FakeFormEngine();
        await using var library = CurlGlobal.InitCounting(engine, CurlGlobalFlags.Default, new GlobalInitCounter());
        CurlForm? form = null;

        var code = CurlForm.Add(library, ref form, new CurlFormArgument(CurlFormOption.CopyName, "name"));

        Assert.AreEqual(CurlFormAddCode.Ok, code);
        Assert.IsNotNull(form);
        Assert.IsFalse(form.IsFreed);
        Assert.HasCount(1, engine.Forms.Single().Adds);
    }

    [TestMethod]
    public async Task Add_AnExistingForm_AddsToItsBuilderWithoutANewOne()
    {
        var engine = new FakeFormEngine();
        await using var library = CurlGlobal.InitCounting(engine, CurlGlobalFlags.Default, new GlobalInitCounter());
        CurlForm? form = null;
        CurlForm.Add(library, ref form, new CurlFormArgument(CurlFormOption.CopyName, "a"));
        var first = form;

        CurlForm.Add(library, ref form, new CurlFormArgument(CurlFormOption.CopyName, "b"));

        Assert.AreSame(first, form);
        Assert.HasCount(2, engine.Forms.Single().Adds);
    }

    [TestMethod]
    public async Task Add_AFailedFirstCall_LeavesTheFormNullAndAnswersTheEngine()
    {
        var engine = new FakeFormEngine { Result = CurlFormAddCode.Incomplete };
        await using var library = CurlGlobal.InitCounting(engine, CurlGlobalFlags.Default, new GlobalInitCounter());
        CurlForm? form = null;

        var code = CurlForm.Add(library, ref form, new CurlFormArgument(CurlFormOption.CopyName, "a"));

        Assert.AreEqual(CurlFormAddCode.Incomplete, code);
        Assert.IsNull(form);
    }

    [TestMethod]
    public async Task Add_AFailedLaterCall_KeepsTheFormAndAnswersTheEngine()
    {
        var builder = new FakeFormBuilder { Result = CurlFormAddCode.OptionTwice };
        CurlForm? form = new CurlForm(builder);
        var before = form;
        await using var library = CurlGlobal.InitCounting(new FakeCurlEngine(), CurlGlobalFlags.Default, new GlobalInitCounter());

        var code = CurlForm.Add(library, ref form, new CurlFormArgument(CurlFormOption.CopyName, "a"));

        Assert.AreEqual(CurlFormAddCode.OptionTwice, code);
        Assert.AreSame(before, form);
    }

    [TestMethod]
    [DataRow(CurlFormOption.Nothing)]
    [DataRow(CurlFormOption.CopyName)]
    [DataRow(CurlFormOption.PointerName)]
    [DataRow(CurlFormOption.NameLength)]
    [DataRow(CurlFormOption.CopyContents)]
    [DataRow(CurlFormOption.PointerContents)]
    [DataRow(CurlFormOption.ContentsLength)]
    [DataRow(CurlFormOption.FileContent)]
    [DataRow(CurlFormOption.Array)]
    [DataRow(CurlFormOption.Obsolete)]
    [DataRow(CurlFormOption.File)]
    [DataRow(CurlFormOption.Buffer)]
    [DataRow(CurlFormOption.BufferPointer)]
    [DataRow(CurlFormOption.BufferLength)]
    [DataRow(CurlFormOption.ContentType)]
    [DataRow(CurlFormOption.ContentHeader)]
    [DataRow(CurlFormOption.FileName)]
    [DataRow(CurlFormOption.End)]
    [DataRow(CurlFormOption.Obsolete2)]
    [DataRow(CurlFormOption.Stream)]
    [DataRow(CurlFormOption.ContentLength)]
    public void Add_EachFormOption_ReachesTheEngineWithItsValueAsGiven(CurlFormOption option)
    {
        var builder = new FakeFormBuilder();
        CurlForm? form = new CurlForm(builder);
        var value = new object();
        var argument = new CurlFormArgument(option, value);

        CurlForm.Add(null!, ref form, new CurlFormArgument(CurlFormOption.CopyName, "field"), argument);

        var passed = builder.Adds.Single();
        Assert.HasCount(2, passed);
        Assert.AreEqual(option, passed[1].Option);
        Assert.AreSame(value, passed[1].Value);
    }

    [TestMethod]
    public void Add_TypedValues_ReachTheEngineAsGiven()
    {
        var builder = new FakeFormBuilder();
        CurlForm? form = new CurlForm(builder);
        byte[] buffer = [1, 2, 3];
        IReadOnlyList<string> headers = ["X-A: 1"];
        IReadOnlyList<CurlFormArgument> array = [new(CurlFormOption.CopyContents, "x")];
        CurlFormArgument[] arguments =
        [
            new(CurlFormOption.CopyName, "field"),
            new(CurlFormOption.BufferPointer, buffer),
            new(CurlFormOption.BufferLength, 3L),
            new(CurlFormOption.ContentHeader, headers),
            new(CurlFormOption.Array, array),
        ];

        CurlForm.Add(null!, ref form, arguments);

        CollectionAssert.AreEqual(arguments, builder.Adds.Single().ToArray());
    }

    [TestMethod]
    public async Task Add_ACleanedUpLibraryAndNoForm_ThrowsObjectDisposed()
    {
        var library = CurlGlobal.InitCounting(new FakeFormEngine(), CurlGlobalFlags.Default, new GlobalInitCounter());
        await library.DisposeAsync();
        CurlForm? form = null;

        Assert.ThrowsExactly<ObjectDisposedException>(() => CurlForm.Add(library, ref form));
    }

    [TestMethod]
    public void Add_AFreedForm_ThrowsObjectDisposed()
    {
        var builder = new FakeFormBuilder();
        CurlForm? form = new CurlForm(builder);
        CurlForm.Free(form);

        Assert.ThrowsExactly<ObjectDisposedException>(() => CurlForm.Add(null!, ref form, new CurlFormArgument(CurlFormOption.CopyName, "a")));
        Assert.IsEmpty(builder.Adds);
    }

    [TestMethod]
    public void Get_PassesTheCallbackToTheEngineAndAnswersItsResult()
    {
        var builder = new FakeFormBuilder { SerializeResult = 23 };
        using var form = new CurlForm(builder);
        FormGetCallback append = buffer => buffer.Length;

        var result = form.Get(append);

        Assert.AreEqual(23, result);
        Assert.AreSame(append, builder.Serializers.Single());
    }

    [TestMethod]
    public void Get_AFreedForm_ThrowsObjectDisposed()
    {
        var form = new CurlForm(new FakeFormBuilder());
        form.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => form.Get(buffer => buffer.Length));
    }

    [TestMethod]
    public void Free_AForm_FreesIt()
    {
        var form = new CurlForm(new FakeFormBuilder());

        CurlForm.Free(form);
        CurlForm.Free(form);

        Assert.IsTrue(form.IsFreed);
    }

    [TestMethod]
    public void Free_Null_DoesNothing()
    {
        CurlForm? form = null;

        CurlForm.Free(form);

        Assert.IsNull(form);
    }
}
