using System.Text;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib557Tests
{
    private const string Case557Output =
        "All curl_mprintf() unsigned short tests OK!\n" +
        "All curl_mprintf() signed short tests OK!\n" +
        "All curl_mprintf() unsigned int tests OK!\n" +
        "All curl_mprintf() signed int tests OK!\n" +
        "All curl_mprintf() unsigned long tests OK!\n" +
        "All curl_mprintf() signed long tests OK!\n" +
        "All curl_mprintf() curl_off_t tests OK!\n" +
        "All curl_mprintf() strings tests OK!\n" +
        "All float strings tests OK!\n" +
        "All curl_mprintf() octal and hexadecimal tests OK!\n" +
        "testing a string and -443 is super fun and 9876543\n" +
        "All v-functions test OK!\n";

    [TestMethod]
    public async Task RunAsync_Case557_PrintsEveryOkLineAndSucceeds()
    {
        var run = new PortRun(null, 557);

        LibTestResult result = await new Lib557().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(Case557Output, run.OutputText);
        Assert.AreEqual("testing a string and -443 is super fun and 9876543\n", run.Error.ToString());
        Assert.AreEqual("lib557", new Lib557().ProgramName);
    }

    [TestMethod]
    public void Groups_ThisPlatformsLongWidth_EveryCheckPasses()
    {
        using var output = new MemoryStream();

        int failed = Lib557.Groups(OperatingSystem.IsWindows()).Sum(group => Lib557.CheckGroup(output, group));

        Assert.AreEqual(0, failed, Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    [DataRow(true, "4294967295")]
    [DataRow(false, "18446744073709551615")]
    public void Groups_LongWidth_PicksTheUnsignedLongTable(bool longIsThirtyTwoBits, string expectedMaximum)
    {
        Lib557Group unsignedLong = Lib557.Groups(longIsThirtyTwoBits).Single(group => group.Name == "unsigned long");

        Assert.AreEqual(expectedMaximum, unsignedLong.Checks[0].Expected);
        Assert.AreEqual("%lu", unsignedLong.Checks[0].Format);
    }

    [TestMethod]
    public void Groups_SignedShort_HoldsTheMaximumAndTheMinimumLessNothing()
    {
        IReadOnlyList<Lib557Check> checks = Lib557.Groups(true).Single(group => group.Name == "signed short").Checks;

        Assert.AreEqual("32767", checks[0].Expected);
        Assert.IsTrue(checks.Any(check => check.Expected == "-32768"));
        Assert.AreEqual("-1", checks[^1].Expected);
    }

    [TestMethod]
    public void CheckGroup_FailingCheck_PrintsItsFailureAndTheFailedLine()
    {
        using var output = new MemoryStream();
        var group = new Lib557Group("unsigned int", "OK\n", "Failed\n", [new Lib557Check("%u", [7], 256, "8"), new Lib557Check("%u", [8], 256, "8")]);

        int failed = Lib557.CheckGroup(output, group);

        Assert.AreEqual(1, failed);
        Assert.AreEqual("unsigned int test #01: Failed (Expected: 8 Got: 7 (1))\nFailed\n", Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    public void CheckGroup_SilentGroupFailing_PrintsOnlyTheFailure()
    {
        using var output = new MemoryStream();
        var group = new Lib557Group("return codes", null, null, [new Lib557Check("%d", [12], 100, string.Empty, Count: 3)]);

        int failed = Lib557.CheckGroup(output, group);

        Assert.AreEqual(1, failed);
        Assert.AreEqual("return codes test #01: Failed (Expected: 3 Got: 12 (2))\n", Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    public void CheckGroup_SilentGroupPassing_PrintsNothing()
    {
        using var output = new MemoryStream();

        int failed = Lib557.CheckGroup(output, new Lib557Group("positional", null, null, [new Lib557Check("%d", [1], 8, "1")]));

        Assert.AreEqual(0, failed);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public void CheckGroup_NullArguments_Throw()
    {
        var group = new Lib557Group("x", null, null, []);

        Assert.ThrowsExactly<ArgumentNullException>(() => Lib557.CheckGroup(null!, group));
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib557.CheckGroup(Stream.Null, null!));
    }

    [TestMethod]
    public void CheckVVersions_SnprintfBufferTooShort_ReportsTheFailure()
    {
        using var output = new MemoryStream();
        using var error = new StringWriter();

        int errors = Lib557.CheckVVersions(output, error, 10);

        Assert.AreEqual(1, errors);
        Assert.AreEqual(
            "testing a string and -443 is super fun and 9876543\nSome v-functions tests Failed!\n",
            Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    public void CheckVVersions_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib557.CheckVVersions(null!, TextWriter.Null, 256));
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib557.CheckVVersions(Stream.Null, null!, 256));
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Lib557().RunAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(3, 20)]
    public void ResultFor_Errors_IsOkOrMajorBad(int errors, int expectedExitCode)
    {
        Assert.AreEqual(expectedExitCode, Lib557.ResultFor(errors).ExitCode);
    }

    [TestMethod]
    public void VVersionsArguments_AreLib557s()
    {
        CollectionAssert.AreEqual(new object?[] { "a string", -443, "super fun", 9876543L }, Lib557.VVersionsArguments.ToArray());
    }
}

[TestClass]
public sealed class Lib557CheckTests
{
    [TestMethod]
    [DataRow(4, "", true)]
    [DataRow(5, "", false)]
    public void Matches_Count_ComparesOnlyTheReturnValue(int count, string text, bool expected)
    {
        Assert.AreEqual(expected, new Lib557Check("%d", [], 8, "ignored", Count: 4).Matches(count, text));
    }

    [TestMethod]
    [DataRow("1.2345e+00", true)]
    [DataRow("1.2345E+00", false)]
    [DataRow("2.2345e+00", false)]
    public void Matches_Suffix_ComparesBothEnds(string text, bool expected)
    {
        Assert.AreEqual(expected, new Lib557Check("%e", [], 8, "1.23", "e+00").Matches(0, text));
    }

    [TestMethod]
    [DataRow("foo", true)]
    [DataRow("foo ", false)]
    public void Matches_Text_ComparesExactly(string text, bool expected)
    {
        Assert.AreEqual(expected, new Lib557Check("%s", [], 8, "foo").Matches(0, text));
    }

    [TestMethod]
    public void Matches_NullText_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib557Check("%s", [], 8, "foo").Matches(0, null!));
    }

    [TestMethod]
    public void Described_EachKind_ShowsWhatTheCheckWants()
    {
        Assert.AreEqual("4", new Lib557Check("%d", [], 8, "x", Count: 4).Described);
        Assert.AreEqual("1.23...e+00", new Lib557Check("%e", [], 8, "1.23", "e+00").Described);
        Assert.AreEqual("foo", new Lib557Check("%s", [], 8, "foo").Described);
    }
}
