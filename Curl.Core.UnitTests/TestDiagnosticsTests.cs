using Curl.Core.Fakes;
using Curl.Testing;

namespace Curl.Core;

/// <summary>
/// Pins the line format of the shared <see cref="TestDiagnostics" /> linked into every
/// test project (BL-1457, ADR-0416), driven by a fake clock so no test waits.
/// </summary>
[TestClass]
public sealed class TestDiagnosticsTests
{
    private const string TestName = "Curl.Core.SampleTests.Sample";

    private static readonly DateTimeOffset Start = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void WriteStart_DataRowTest_WritesClassTestAndDisplayName()
    {
        var (diagnostics, lines, _) = Create();

        diagnostics.WriteStart("Curl.Core.SampleTests", "Sample", "Sample (1)");

        Report(lines);
        CollectionAssert.AreEqual(new[] { "START Curl.Core.SampleTests.Sample (Sample (1))" }, lines);
    }

    [TestMethod]
    public void WriteEnd_At3001Milliseconds_WritesSlowLineAfterEndLine()
    {
        var (diagnostics, lines, clock) = Create();
        clock.Advance(TimeSpan.FromMilliseconds(3001));

        diagnostics.WriteEnd(TestName, "Passed");

        Report(lines);
        CollectionAssert.AreEqual(
            new[]
            {
                $"END {TestName}: Passed in 3001 ms (arrange 0, act 0, assert 0)",
                $"SLOW: {TestName} took 3001 ms (budget 3000 ms)",
            },
            lines);
    }

    [TestMethod]
    [DataRow(3000)]
    [DataRow(2999)]
    public void WriteEnd_AtOrUnderBudget_WritesNoSlowLine(int milliseconds)
    {
        var (diagnostics, lines, clock) = Create();
        clock.Advance(TimeSpan.FromMilliseconds(milliseconds));

        diagnostics.WriteEnd(TestName, "Passed");

        Report(lines);
        CollectionAssert.AreEqual(
            new[] { $"END {TestName}: Passed in {milliseconds} ms (arrange 0, act 0, assert 0)" },
            lines);
    }

    [TestMethod]
    public void FormatSlow_ByElapsedTime_IsWrittenOnlyOverBudget()
    {
        Assert.IsNull(TestDiagnostics.FormatSlow(TestName, TimeSpan.FromMilliseconds(3000.9)));
        Assert.AreEqual(
            $"SLOW: {TestName} took 3001 ms (budget 3000 ms)",
            TestDiagnostics.FormatSlow(TestName, TimeSpan.FromMilliseconds(3001)));
    }

    [TestMethod]
    public void WriteEnd_AfterArrangeActAssertAndDiff_CountsEachKind()
    {
        var (diagnostics, lines, clock) = Create();
        diagnostics.Arrange("command line", "curl file:///dir/x");
        diagnostics.Arrange("url", "file:///dir/x");
        diagnostics.Act("exit code", 0);
        diagnostics.Assert("exit code", 0, 0);
        diagnostics.Diff("stdout", "abc", "abc");
        diagnostics.Diff("body", [1, 2], [1, 2]);
        diagnostics.Bytes("body", [1, 2]);
        clock.Advance(TimeSpan.FromMilliseconds(12));

        diagnostics.WriteEnd(TestName, "Failed");

        Report(lines);
        Assert.AreEqual("ARRANGE command line: curl file:///dir/x", lines[0]);
        Assert.AreEqual("ACT exit code: 0", lines[2]);
        Assert.AreEqual("ASSERT exit code: expected 0, actual 0", lines[3]);
        Assert.AreEqual($"END {TestName}: Failed in 12 ms (arrange 2, act 1, assert 3)", lines[^1]);
    }

    [TestMethod]
    public void FormatStart_DisplayNameDiffers_AppendsIt()
    {
        Assert.AreEqual("START A.B.Test", TestDiagnostics.FormatStart("A.B", "Test", "Test"));
        Assert.AreEqual("START A.B.Test", TestDiagnostics.FormatStart("A.B", "Test", null));
        Assert.AreEqual("START A.B.Test (Test (1,2))", TestDiagnostics.FormatStart("A.B", "Test", "Test (1,2)"));
    }

    [TestMethod]
    public void FormatBytes_PrintableAndNonPrintable_ShowsHexAndDotsForNonPrintable()
    {
        var line = TestDiagnostics.FormatBytes("request", "GET\r\n"u8.ToArray().Append((byte)0x7f).Append((byte)0xff).ToArray());

        TestDiagnostics.For(TestContext).Act("line", line);
        Assert.AreEqual("BYTES request (7 bytes): 47 45 54 0d 0a 7f ff | GET....", line);
    }

    [TestMethod]
    public void FormatBytes_Empty_ShowsZeroBytes() =>
        Assert.AreEqual("BYTES empty (0 bytes):  | ", TestDiagnostics.FormatBytes("empty", []));

    [TestMethod]
    public void FormatBytes_PastTheCap_ShowsTheCapAndCountsTheRest()
    {
        var bytes = Enumerable.Repeat((byte)'a', TestDiagnostics.ByteDisplayCap + 4).ToArray();

        var line = TestDiagnostics.FormatBytes("body", bytes);

        var hex = string.Join(' ', Enumerable.Repeat("61", TestDiagnostics.ByteDisplayCap));
        var text = new string('a', TestDiagnostics.ByteDisplayCap);
        Assert.AreEqual($"BYTES body (260 bytes): {hex} | {text} ... (4 more bytes)", line);
    }

    [TestMethod]
    public void FormatDiff_Bytes_FirstDifference_NamesIndexAndBothBytes() =>
        Assert.AreEqual(
            "DIFF body: first difference at byte 2: expected 0x63 'c', actual 0x0a '.'",
            TestDiagnostics.FormatDiff("body", "abc"u8, "ab\n"u8));

    [TestMethod]
    public void FormatDiff_Bytes_LengthsDiffer_NamesBothLengths()
    {
        Assert.AreEqual(
            "DIFF body: lengths differ, expected 3 bytes, actual 2 bytes, equal for the first 2",
            TestDiagnostics.FormatDiff("body", "abc"u8, "ab"u8));
        Assert.AreEqual(
            "DIFF body: lengths differ, expected 1 bytes, actual 2 bytes, equal for the first 1",
            TestDiagnostics.FormatDiff("body", "a"u8, "ab"u8));
    }

    [TestMethod]
    public void FormatDiff_Bytes_Equal_SaysEqual() =>
        Assert.AreEqual("DIFF body: equal (3 bytes)", TestDiagnostics.FormatDiff("body", "abc"u8, "abc"u8));

    [TestMethod]
    public void FormatDiff_Strings_FirstDifference_NamesCharacterIndex() =>
        Assert.AreEqual(
            "DIFF stderr: first difference at character 1: expected 0x62 'b', actual 0x09 '.'",
            TestDiagnostics.FormatDiff("stderr", "abc", "a\tc"));

    [TestMethod]
    public void FormatDiff_Strings_LengthsDifferAndEqual()
    {
        Assert.AreEqual(
            "DIFF stderr: lengths differ, expected 2 characters, actual 3 characters, equal for the first 2",
            TestDiagnostics.FormatDiff("stderr", "ab", "abc"));
        Assert.AreEqual("DIFF stderr: equal (2 characters)", TestDiagnostics.FormatDiff("stderr", "ab", "ab"));
    }

    [TestMethod]
    public void Phase_Disposed_WritesNameAndMilliseconds()
    {
        var (diagnostics, lines, clock) = Create();

        using (diagnostics.Phase("handshake"))
        {
            clock.Advance(TimeSpan.FromMilliseconds(42));
        }

        Report(lines);
        CollectionAssert.AreEqual(new[] { "PHASE handshake: 42 ms" }, lines);
    }

    [TestMethod]
    public void For_SameTestContext_ReturnsTheDiagnosticsTheGlobalHookStarted()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        Assert.AreSame(diagnostics, TestDiagnostics.For(TestContext));
        Assert.IsTrue(diagnostics.Elapsed >= TimeSpan.Zero);
    }

    private static (TestDiagnostics Diagnostics, List<string> Lines, FakeTimeProvider Clock) Create()
    {
        var lines = new List<string>();
        var clock = new FakeTimeProvider(Start);
        return (new TestDiagnostics(lines.Add, clock), lines, clock);
    }

    private void Report(List<string> lines)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        foreach (var line in lines)
        {
            diagnostics.Act("captured line", line);
        }
    }
}
