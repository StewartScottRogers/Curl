using Curl.Api.Codes;
using Libcurl.Global;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib1560Tests
{
    [TestMethod]
    public async Task RunAsync_SchemeGuessFails_AnswersThirteenWithoutSuccess()
    {
        var run = new PortRun(new FakeCurlEngine(), 1560);

        LibTestResult result = await new Lib1560(isWindows: true).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(Lib1560.SchemeGuessFailed, result.ExitCode);
        Assert.AreEqual("lib1560", new Lib1560(isWindows: false).ProgramName);
        Assert.AreEqual(string.Empty, run.OutputText);
        StringAssert.Contains(run.Error.ToString(), "0: got no scheme when https was expected\n");
    }

    [TestMethod]
    public async Task TestAsync_CleanedUpLibrary_AnswersThirteen()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 1560);

        await Lib1560.TestAsync(run.Context, run.Steps, library, isWindows: true);

        Assert.AreEqual((CurlCode)Lib1560.SchemeGuessFailed, run.Steps.Result);
    }

    [TestMethod]
    public void Groups_OnAFakeHandle_RunInUpstreamsOrderAndEachFails()
    {
        var checks = new Lib1560Checks(CurlGlobal.Init(new FakeCurlEngine()), new StringWriter(), new StringWriter(), isWindows: true, hasUtf8: false);

        IReadOnlyList<(Func<int> Check, int Failed)> groups = Lib1560.Groups(checks);

        CollectionAssert.AreEqual(new[] { 13, 12, 11, 10, 3, 9, 7, 6, 5, 1, 2, 4, 8 }, groups.Select(group => group.Failed).ToArray());
        CollectionAssert.AreEqual(new[] { 13, 12, 11, 10, 3, 9, 7, 6, 5, 1, 2, 4, 8 }, groups.Where(group => group.Check() != 0).Select(group => group.Failed).ToArray());
    }

    [TestMethod]
    public void RunGroups_EveryGroupPasses_WritesSuccess()
    {
        var output = new StringWriter();

        int failed = Lib1560.RunGroups(output, [(() => 0, 13), (() => 0, 12)]);

        Assert.AreEqual(0, failed);
        Assert.AreEqual("success\n", output.ToString());
    }

    [TestMethod]
    public void RunGroups_SecondGroupFails_AnswersItsNumberAndSkipsTheRest()
    {
        var output = new StringWriter();
        bool thirdRan = false;

        int failed = Lib1560.RunGroups(output, [(() => 0, 13), (() => 2, 12), (() => { thirdRan = true; return 0; }, 11)]);

        Assert.AreEqual(12, failed);
        Assert.IsFalse(thirdRan);
        Assert.AreEqual(string.Empty, output.ToString());
    }

    [TestMethod]
    public void Tables_HoldUpstreamsRowCounts()
    {
        Assert.HasCount(180, Lib1560Tables.GetParts);
        Assert.HasCount(218, Lib1560Tables.GetUrls);
        Assert.HasCount(6, Lib1560Tables.SetGetParts);
        Assert.HasCount(61, Lib1560Tables.SetParts);
        Assert.HasCount(62, Lib1560Tables.SetUrls);
        Assert.HasCount(7, Lib1560Tables.Appends);
        Assert.HasCount(18, Lib1560Tables.SchemeGuesses);
        Assert.HasCount(10, Lib1560Tables.ClearUrls);
        Assert.AreEqual(3, Lib1560Tables.GetParts.Count(row => row.WindowsOnly));
    }
}
