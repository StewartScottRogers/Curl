using Curl.Api.Codes;
using Curl.Api.Info;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed partial class LibTestStepsTests
{
    private const CurlOption UnknownOption = (CurlOption)99999;

    [TestMethod]
    public void Constructor_RefusesANullWriter()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new LibTestSteps(null!));
    }

    [TestMethod]
    public void Result_StartsOkAndToLibTestResultGivesItsNumber()
    {
        var steps = new LibTestSteps(new StringWriter());

        Assert.AreEqual(CurlCode.Ok, steps.Result);
        steps.Result = LibTestErrorCodes.Usage;

        Assert.AreEqual(46, steps.ToLibTestResult().ExitCode);
    }

    [TestMethod]
    public async Task GlobalInit_WithAnEngine_GoesOnWithALibraryOfTheFlags()
    {
        var steps = new LibTestSteps(new StringWriter());

        bool goesOn = steps.GlobalInit(new FakeCurlEngine(), CurlGlobalFlags.All, out CurlLibrary? library);

        Assert.IsTrue(goesOn);
        Assert.IsNotNull(library);
        Assert.AreEqual(CurlGlobalFlags.All, library.Flags);
        Assert.AreEqual(CurlCode.Ok, steps.Result);
        await library.DisposeAsync();
    }

    [TestMethod]
    public void GlobalInit_WithoutAnEngine_FailsWithFailedInitAndReportsIt()
    {
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        bool goesOn = steps.GlobalInit(null, CurlGlobalFlags.All, out CurlLibrary? library, "/src/lib500.c", 36);

        Assert.IsFalse(goesOn);
        Assert.IsNull(library);
        Assert.AreEqual(CurlCode.FailedInit, steps.Result);
        Assert.AreEqual(2, steps.ToLibTestResult().ExitCode);
        Assert.AreEqual("lib500.c:36 curl_global_init() failed, with code 2 (Failed initialization)\n", error.ToString());
    }

    [TestMethod]
    public async Task EasyInit_OnALiveLibrary_GoesOnWithAHandleAndKeepsResult()
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        var error = new StringWriter();
        var steps = new LibTestSteps(error) { Result = LibTestErrorCodes.Failure };

        bool goesOn = steps.EasyInit(library, out CurlEasy? easy);

        Assert.IsTrue(goesOn);
        Assert.IsNotNull(easy);
        Assert.AreEqual(LibTestErrorCodes.Failure, steps.Result);
        Assert.AreEqual(string.Empty, error.ToString());
        await easy.DisposeAsync();
    }

    [TestMethod]
    public async Task EasyInit_OnACleanedUpLibrary_FailsWithTestErrEasyInit()
    {
        CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await library.DisposeAsync();
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        bool goesOn = steps.EasyInit(library, out CurlEasy? easy, "lib500.c", 41);

        Assert.IsFalse(goesOn);
        Assert.IsNull(easy);
        Assert.AreEqual(LibTestErrorCodes.EasyInit, steps.Result);
        Assert.AreEqual(29, steps.ToLibTestResult().ExitCode);
        Assert.AreEqual("lib500.c:41 curl_easy_init() failed\n", error.ToString());
    }

    [TestMethod]
    public void EasyInit_RefusesANullLibrary()
    {
        var steps = new LibTestSteps(new StringWriter());

        Assert.ThrowsExactly<ArgumentNullException>(() => steps.EasyInit(null!, out _));
    }

    [TestMethod]
    public async Task EasySetOption_ThatSucceeds_GoesOnAndSetsTheOption()
    {
        string? url = null;
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine { Performing = o => { url = o.Network.Url; return CurlCode.Ok; } });
        await using CurlEasy easy = library.EasyInit();
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        bool goesOn = steps.EasySetOption(easy, CurlOption.Url, "http://127.0.0.1/500");
        await easy.PerformAsync();

        Assert.IsTrue(goesOn);
        Assert.AreEqual("http://127.0.0.1/500", url);
        Assert.AreEqual(CurlCode.Ok, steps.Result);
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task EasySetOption_ThatFails_TakesSetoptsCodeAndReportsIt()
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await using CurlEasy easy = library.EasyInit();
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        bool goesOn = steps.EasySetOption(easy, UnknownOption, 1L, "lib500.c", 48);

        Assert.IsFalse(goesOn);
        Assert.AreEqual(CurlCode.UnknownOption, steps.Result);
        Assert.AreEqual(48, steps.ToLibTestResult().ExitCode);
        Assert.AreEqual("lib500.c:48 curl_easy_setopt() failed, with code 48 (An unknown option was passed in to libcurl)\n", error.ToString());
    }

    [TestMethod]
    public void EasySetOption_RefusesANullHandle()
    {
        var steps = new LibTestSteps(new StringWriter());

        Assert.ThrowsExactly<ArgumentNullException>(() => steps.EasySetOption(null!, CurlOption.Url, "x"));
    }

    [TestMethod]
    public async Task EasyPerformAsync_ThatSucceeds_GoesOnWithResultOk()
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await using CurlEasy easy = library.EasyInit();
        var steps = new LibTestSteps(new StringWriter()) { Result = LibTestErrorCodes.Failure };

        bool goesOn = await steps.EasyPerformAsync(easy, CancellationToken.None);

        Assert.IsTrue(goesOn);
        Assert.AreEqual(CurlCode.Ok, steps.Result);
    }

    [TestMethod]
    public async Task EasyPerformAsync_ThatFails_TakesTheTransfersCodeAndWritesNothing()
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine { PerformResult = CurlCode.CouldntConnect });
        await using CurlEasy easy = library.EasyInit();
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        bool goesOn = await steps.EasyPerformAsync(easy, CancellationToken.None);

        Assert.IsFalse(goesOn);
        Assert.AreEqual(CurlCode.CouldntConnect, steps.Result);
        Assert.AreEqual(7, steps.ToLibTestResult().ExitCode);
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task EasyPerformAsync_RefusesANullHandle()
    {
        var steps = new LibTestSteps(new StringWriter());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await steps.EasyPerformAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task EasyGetInfo_ThatSucceeds_GoesOnWithTheValue()
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine { Info = new TransferInfo { ResponseCode = 200 } });
        await using CurlEasy easy = library.EasyInit();
        var steps = new LibTestSteps(new StringWriter());

        bool goesOn = steps.EasyGetInfo(easy, CurlInfo.ResponseCode, out object? value);

        Assert.IsTrue(goesOn);
        Assert.AreEqual(200L, value);
        Assert.AreEqual(CurlCode.Ok, steps.Result);
    }

    [TestMethod]
    public async Task EasyGetInfo_ThatFails_TakesGetinfosCodeAndWritesNothing()
    {
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await using CurlEasy easy = library.EasyInit();
        var error = new StringWriter();
        var steps = new LibTestSteps(error);

        bool goesOn = steps.EasyGetInfo(easy, (CurlInfo)0, out object? value);

        Assert.IsFalse(goesOn);
        Assert.IsNull(value);
        Assert.AreEqual(CurlCode.UnknownOption, steps.Result);
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public void EasyGetInfo_RefusesANullHandle()
    {
        var steps = new LibTestSteps(new StringWriter());

        Assert.ThrowsExactly<ArgumentNullException>(() => steps.EasyGetInfo(null!, CurlInfo.ResponseCode, out _));
    }

    [TestMethod]
    public void SlistAppend_WithText_StartsThenGrowsTheListAndKeepsResult()
    {
        var steps = new LibTestSteps(new StringWriter()) { Result = LibTestErrorCodes.Failure };
        CurlSlist? list = null;

        bool first = steps.SlistAppend(ref list, "Accept: */*");
        bool second = steps.SlistAppend(ref list, "X-Test: 1");

        Assert.IsTrue(first);
        Assert.IsTrue(second);
        Assert.IsNotNull(list);
        CollectionAssert.AreEqual(new[] { "Accept: */*", "X-Test: 1" }, list.ToArray());
        Assert.AreEqual(LibTestErrorCodes.Failure, steps.Result);
    }

    [TestMethod]
    public void SlistAppend_WithoutText_FailsWithTestErrMajorBadAndLeavesTheList()
    {
        var error = new StringWriter();
        var steps = new LibTestSteps(error);
        CurlSlist? list = CurlSlist.Append(null, "Accept: */*");

        bool goesOn = steps.SlistAppend(ref list, null, "lib1501.c", 12);

        Assert.IsFalse(goesOn);
        Assert.IsNotNull(list);
        Assert.HasCount(1, list);
        Assert.AreEqual(LibTestErrorCodes.MajorBad, steps.Result);
        Assert.AreEqual(20, steps.ToLibTestResult().ExitCode);
        Assert.AreEqual("lib1501.c:12 curl_slist_append() failed\n", error.ToString());
    }
}
