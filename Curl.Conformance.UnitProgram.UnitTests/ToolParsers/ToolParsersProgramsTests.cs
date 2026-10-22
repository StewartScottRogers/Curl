using System.Globalization;
using System.Text;
using Curl.Conformance.UnitProgram.Hashes;
using Curl.Conformance.UnitProgram.ToolParsers;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.UnitProgram.UnitTests.ToolParsers;

[TestClass]
public sealed class ToolParsersProgramsTests
{
    private string? temporaryDirectory;

    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object[]> ProgramsWithoutArguments =>
    [
        ["tool1604"], ["tool1621"], ["tool1622"], ["tool1623"], ["unit1636"], ["unit1668"], ["tool1720"], ["unit2604"], ["unit2605"], ["unit3227"],
    ];

    public static IEnumerable<object[]> AllPrograms => [.. ProgramsWithoutArguments, ["unit3200"]];

    [TestMethod]
    [DynamicData(nameof(ProgramsWithoutArguments))]
    public async Task RunAsync_PortedProgram_EveryCheckPassesAndNothingIsWritten(string programName)
    {
        IUnitProgram program = UnitPrograms.Ported.Find(programName)!;
        using StringWriter standardError = new();

        UnitProgramResult result = await program.RunAsync(Context(programName, [], standardError, Stream.Null), TestContext.CancellationToken);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0, result.ExitCode);
    }

    [TestMethod]
    [DynamicData(nameof(AllPrograms))]
    public async Task RunAsync_NullContext_Throws(string programName)
    {
        IUnitProgram program = UnitPrograms.Ported.Find(programName)!;

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => program.RunAsync(null!, TestContext.CancellationToken));
    }

    [TestMethod]
    [DataRow("tool1622", "timebuf", "                   1 - 00:00:01", "    1099445657078333 -  999T")]
    [DataRow("unit1636", "time2str", "                   1 -   00:01", "    1099445657078333 - 999.9T")]
    public async Task RunAsync_ProgressProgram_WritesTheCaseStdoutLines(string programName, string firstLine, string secondLine, string lastLine)
    {
        using MemoryStream standardOutput = new();

        await UnitPrograms.Ported.Find(programName)!.RunAsync(Context(programName, [], TextWriter.Null, standardOutput), TestContext.CancellationToken);

        string[] lines = Encoding.ASCII.GetString(standardOutput.ToArray()).Split('\n');
        Assert.HasCount(134, lines);
        Assert.AreEqual(firstLine, lines[0]);
        Assert.AreEqual(secondLine, lines[2]);
        Assert.AreEqual(lastLine, lines[132]);
        Assert.AreEqual(string.Empty, lines[133]);
    }

    [TestMethod]
    public async Task RunAsync_Unit2605_WritesEachRowAndOk()
    {
        using MemoryStream standardOutput = new();

        await new Unit2605SshRange().RunAsync(Context("unit2605", [], TextWriter.Null, standardOutput), TestContext.CancellationToken);

        string text = Encoding.ASCII.GetString(standardOutput.ToArray());
        Assert.StartsWith("0: '0-9' (file size: 100)\n", text);
        Assert.EndsWith("25: '100-' (file size: 100)\nok\n", text);
    }

    [TestMethod]
    public async Task WriteRowAsync_TextOfTheWrongWidth_WritesTooLong()
    {
        using MemoryStream standardOutput = new();
        StandardOutputLines output = new(Context("unit1636", [], TextWriter.Null, standardOutput));

        await Unit1636ProgressMeterFields.WriteRowAsync(output, 5, "12345678", 7, TestContext.CancellationToken);

        Assert.AreEqual("                   5 - 12345678\n^^ was too long!\n", Encoding.ASCII.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task WriteRowAsync_NullArguments_Throw()
    {
        StandardOutputLines output = new(new UnitProgramContext(1636, []));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Unit1636ProgressMeterFields.WriteRowAsync(null!, 0, "x", 1, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Unit1636ProgressMeterFields.WriteRowAsync(output, 0, null!, 1, TestContext.CancellationToken));
    }

    [TestMethod]
    public void ErrorOf_EachRefusal_IsItsParameterError()
    {
        Assert.AreEqual(Tool1623SizeParameter.ParameterError.Ok, Tool1623SizeParameter.ErrorOf(null));
        Assert.AreEqual(Tool1623SizeParameter.ParameterError.NumberTooLarge, Tool1623SizeParameter.ErrorOf(Cli.CommandLineRefusal.TooLargeNumber("--limit-rate")));
        Assert.AreEqual(Tool1623SizeParameter.ParameterError.BadNumeric, Tool1623SizeParameter.ErrorOf(Cli.CommandLineRefusal.ExpectedProperNumericalParameter("--limit-rate")));
        Assert.AreEqual(Tool1623SizeParameter.ParameterError.BadUse, Tool1623SizeParameter.ErrorOf(Cli.CommandLineRefusal.BadlyUsedHere("--limit-rate")));
    }

    [TestMethod]
    public void CheckRow_ResultThatDiffers_WritesItAndFails()
    {
        using StringWriter standardError = new();

        bool passed = Unit1668Ftp213Date.CheckRow(standardError, 3, ("1998", "", true));

        Assert.IsFalse(passed);
        Assert.AreEqual("test 3: expected result 1, got 0 for 1998" + Environment.NewLine, standardError.ToString());
    }

    [TestMethod]
    public void CheckRow_AcceptedWhereARefusalIsExpected_WritesItAndFails()
    {
        using StringWriter standardError = new();

        bool passed = Unit1668Ftp213Date.CheckRow(standardError, 5, ("19980320114234", "", false));

        Assert.IsFalse(passed);
        Assert.AreEqual("test 5: expected result 0, got 1 for 19980320114234" + Environment.NewLine, standardError.ToString());
    }

    [TestMethod]
    public void CheckRow_DateThatDiffers_WritesItAndFails()
    {
        using StringWriter standardError = new();

        bool passed = Unit1668Ftp213Date.CheckRow(standardError, 4, ("19980320114234", "1998-03-21 11:42:34", true));

        Assert.IsFalse(passed);
        Assert.AreEqual("test 4: (from input 19980320114234) got '1998-03-20 11:42:34' expected '1998-03-21 11:42:34'" + Environment.NewLine, standardError.ToString());
    }

    [TestMethod]
    public void CheckRow_NullStandardError_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => Unit1668Ftp213Date.CheckRow(null!, 0, ("", "", false)));

    [TestMethod]
    public void Trace_PathWithDirectories_ListsEachWithABar()
    {
        Assert.AreEqual("foo|foo/bar|", Tool1720DirectoryHierarchy.Trace("foo/bar/f", runsOnWindows: false));
        Assert.AreEqual("(null)", Tool1720DirectoryHierarchy.Trace("f", runsOnWindows: true));
    }

    [TestMethod]
    public void CheckAnswer_Unit2604AnswerThatDiffers_CountsEachDifference()
    {
        using StringWriter standardError = new();
        UnitChecks checks = new(standardError, "unit2604");

        Unit2604GetPathname.CheckAnswer(checks, (CurlExitCode.Ok, Encoding.UTF8.GetBytes("x"), "y"), "a", "b", CurlExitCode.QuoteError);
        Unit2604GetPathname.CheckAnswer(checks, (CurlExitCode.QuoteError, [], ""), "a", "b", CurlExitCode.Ok);

        Assert.AreEqual(4, checks.Result.FailureCount);
        StringAssert.Contains(standardError.ToString(), "unit2604: ... gave 'x', not 'a' as expected");
        Assert.ThrowsExactly<ArgumentNullException>(() => Unit2604GetPathname.CheckAnswer(null!, (CurlExitCode.Ok, [], ""), "", "", CurlExitCode.Ok));
    }

    [TestMethod]
    public async Task CheckAnswer_Unit2605AnswerThatDiffers_CountsEachDifferenceAndWritesNoOk()
    {
        using StringWriter standardError = new();
        using MemoryStream standardOutput = new();
        UnitChecks checks = new(standardError, "unit2605");

        Unit2605SshRange.CheckAnswer(checks, (CurlExitCode.Ok, 1, 2), 3, 4, CurlExitCode.RangeError);
        Unit2605SshRange.CheckAnswer(checks, (CurlExitCode.RangeError, 0, 0), 3, 4, CurlExitCode.Ok);
        await Unit2605SshRange.WriteOkWhenPassedAsync(new StandardOutputLines(Context("unit2605", [], TextWriter.Null, standardOutput)), checks.Result, TestContext.CancellationToken);

        Assert.AreEqual(4, checks.Result.FailureCount);
        StringAssert.Contains(standardError.ToString(), "unit2605: ... start (1) was not 3");
        Assert.AreEqual(0, standardOutput.Length);
        Assert.ThrowsExactly<ArgumentNullException>(() => Unit2605SshRange.CheckAnswer(null!, (CurlExitCode.Ok, 0, 0), 0, 0, CurlExitCode.Ok));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Unit2605SshRange.WriteOkWhenPassedAsync(null!, checks.Result, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task RunAsync_Unit3200WithItsFile_EveryCheckPassesAndEachCaseSaysOk()
    {
        string path = TemporaryFile();
        using StringWriter standardError = new();

        UnitProgramResult result = await new Unit3200GetLine().RunAsync(Context("unit3200", [path], standardError, Stream.Null), TestContext.CancellationToken);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Concat(Enumerable.Range(0, 6).Select(index => string.Create(CultureInfo.InvariantCulture, $"Test {index}...OK{Environment.NewLine}"))), standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_Unit3200WithoutArguments_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => new Unit3200GetLine().RunAsync(new UnitProgramContext(3200, []), TestContext.CancellationToken));

    [TestMethod]
    public async Task RunAsync_Unit3200CaseThatMissesTheEnd_StopsAtIt()
    {
        string path = TemporaryFile();
        using StringWriter standardError = new();
        GetLineFileCase[] cases = [new("A\nB\n", [("A\n", "first")], EndsAtEndOfFile: true, "Missed EOF"), new("A\n", [], EndsAtEndOfFile: true, "never")];

        UnitProgramResult result = await new Unit3200GetLine(cases).RunAsync(Context("unit3200", [path], standardError, Stream.Null), TestContext.CancellationToken);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual("Test 0...unit3200: Missed EOF" + Environment.NewLine, standardError.ToString());
    }

    [TestMethod]
    public async Task CheckCaseAsync_LinesThatDiffer_CountEachFailure()
    {
        string path = TemporaryFile();
        using StringWriter standardError = new();
        UnitChecks checks = new(standardError, "unit3200");
        GetLineFileCase fileCase = new(new string('x', 5000) + "\nB\nC\n", [("A\n", "too large"), ("A\n", "differs")], EndsAtEndOfFile: false, "not too large");

        bool carriesOn = await Unit3200GetLine.CheckCaseAsync(checks, path, fileCase, TestContext.CancellationToken);

        Assert.IsTrue(carriesOn);
        Assert.AreEqual(3, checks.Result.FailureCount);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Unit3200GetLine.CheckCaseAsync(null!, path, fileCase, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Unit3200GetLine.CheckCaseAsync(checks, path, null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public void HeadersHaveControl_SplitAndCleanBlocks_AreTold()
    {
        Assert.IsTrue(Unit3227MailPartLineBreaks.HeadersHaveControl("A: a\rb\r\n\r\n"));
        Assert.IsFalse(Unit3227MailPartLineBreaks.HeadersHaveControl("A: b\r\n\r\n"));
        Assert.ThrowsExactly<ArgumentNullException>(() => Unit3227MailPartLineBreaks.HeadersHaveControl(null!));
    }

    private static UnitProgramContext Context(string programName, IReadOnlyList<string> arguments, TextWriter standardError, Stream standardOutput) =>
        new(int.Parse(programName[4..], CultureInfo.InvariantCulture), arguments) { StandardError = standardError, StandardOutput = standardOutput };

    [TestCleanup]
    public void DeleteTemporaryDirectory()
    {
        if (temporaryDirectory is not null)
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private string TemporaryFile()
    {
        temporaryDirectory = Path.Combine(Path.GetTempPath(), "unit3200-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        return Path.Combine(temporaryDirectory, "curl_get_line3200");
    }
}
