using Curl.Core.Fakes;
using Curl.Testing;

namespace Curl.Core;

/// <summary>
/// Pins the line format of the shared <see cref="TestDiagnostics" /> linked into every
/// test project (BL-1457, ADR-0417), driven by a fake clock so no test waits.
/// </summary>
[TestClass]
public sealed class TestDiagnosticsTests
{
    private const string TestName = "Curl.Core.SampleTests.Sample";

    private static readonly DateTimeOffset Start = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Log => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void WriteStart_DataRowTest_WritesClassTestAndDisplayName()
    {
        var (diagnostics, lines, _) = Create();
        Log.Arrange("start", "class Curl.Core.SampleTests, method Sample, display name Sample (1)");

        diagnostics.WriteStart("Curl.Core.SampleTests", "Sample", "Sample (1)");

        Report(lines);
        Log.Assert("captured lines", "START Curl.Core.SampleTests.Sample (Sample (1))", string.Join(" | ", lines));
        CollectionAssert.AreEqual(new[] { "START Curl.Core.SampleTests.Sample (Sample (1))" }, lines);
    }

    [TestMethod]
    public void WriteEnd_At3001Milliseconds_WritesSlowLineAfterEndLine()
    {
        var (diagnostics, lines, clock) = Create();
        clock.Advance(TimeSpan.FromMilliseconds(3001));
        Log.Arrange("fake clock advance", "3001 ms");

        diagnostics.WriteEnd(TestName, "Passed");

        Report(lines);
        Log.Assert("captured line count", 2, lines.Count);
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
        Log.Arrange("fake clock advance", $"{milliseconds} ms");

        diagnostics.WriteEnd(TestName, "Passed");

        Report(lines);
        Log.Assert("captured line count", 1, lines.Count);
        CollectionAssert.AreEqual(
            new[] { $"END {TestName}: Passed in {milliseconds} ms (arrange 0, act 0, assert 0)" },
            lines);
    }

    [TestMethod]
    public void FormatSlow_ByElapsedTime_IsWrittenOnlyOverBudget()
    {
        Log.Arrange("elapsed times", "3000.9 ms, 3001 ms");

        var underBudget = TestDiagnostics.FormatSlow(TestName, TimeSpan.FromMilliseconds(3000.9));
        var overBudget = TestDiagnostics.FormatSlow(TestName, TimeSpan.FromMilliseconds(3001));

        Log.Act("3000.9 ms", underBudget ?? "(null)");
        Log.Act("3001 ms", overBudget ?? "(null)");
        Log.Assert("3000.9 ms", "(null)", underBudget ?? "(null)");
        Log.Assert("3001 ms", $"SLOW: {TestName} took 3001 ms (budget 3000 ms)", overBudget);
        Assert.IsNull(underBudget);
        Assert.AreEqual($"SLOW: {TestName} took 3001 ms (budget 3000 ms)", overBudget);
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
        Log.Arrange("written", "2 ARRANGE, 1 ACT, 1 ASSERT, 2 DIFF, 1 BYTES; fake clock advance 12 ms");

        diagnostics.WriteEnd(TestName, "Failed");

        Report(lines);
        Log.Assert("last line", $"END {TestName}: Failed in 12 ms (arrange 2, act 1, assert 3)", lines[^1]);
        Assert.AreEqual("ARRANGE command line: curl file:///dir/x", lines[0]);
        Assert.AreEqual("ACT exit code: 0", lines[2]);
        Assert.AreEqual("ASSERT exit code: expected 0, actual 0", lines[3]);
        Assert.AreEqual($"END {TestName}: Failed in 12 ms (arrange 2, act 1, assert 3)", lines[^1]);
    }

    [TestMethod]
    public void FormatStart_DisplayNameDiffers_AppendsIt()
    {
        Log.Arrange("class and method", "A.B, Test; display names Test, null, Test (1,2)");

        var same = TestDiagnostics.FormatStart("A.B", "Test", "Test");
        var none = TestDiagnostics.FormatStart("A.B", "Test", null);
        var differs = TestDiagnostics.FormatStart("A.B", "Test", "Test (1,2)");

        Log.Act("same display name", same);
        Log.Act("no display name", none);
        Log.Act("different display name", differs);
        Log.Assert("different display name", "START A.B.Test (Test (1,2))", differs);
        Assert.AreEqual("START A.B.Test", same);
        Assert.AreEqual("START A.B.Test", none);
        Assert.AreEqual("START A.B.Test (Test (1,2))", differs);
    }

    [TestMethod]
    public void FormatBytes_PrintableAndNonPrintable_ShowsHexAndDotsForNonPrintable()
    {
        Log.Arrange("bytes", "GET\\r\\n then 0x7f 0xff");

        var line = TestDiagnostics.FormatBytes("request", "GET\r\n"u8.ToArray().Append((byte)0x7f).Append((byte)0xff).ToArray());

        Log.Act("line", line);
        Log.Assert("line", "BYTES request (7 bytes): 47 45 54 0d 0a 7f ff | GET....", line);
        Assert.AreEqual("BYTES request (7 bytes): 47 45 54 0d 0a 7f ff | GET....", line);
    }

    [TestMethod]
    public void FormatBytes_Empty_ShowsZeroBytes()
    {
        Log.Arrange("bytes", "none");

        var line = TestDiagnostics.FormatBytes("empty", []);

        Log.Act("line", line);
        Log.Assert("line", "BYTES empty (0 bytes):  | ", line);
        Assert.AreEqual("BYTES empty (0 bytes):  | ", line);
    }

    [TestMethod]
    public void FormatBytes_PastTheCap_ShowsTheCapAndCountsTheRest()
    {
        var bytes = Enumerable.Repeat((byte)'a', TestDiagnostics.ByteDisplayCap + 4).ToArray();
        Log.Arrange("bytes", $"{bytes.Length} x 'a' (cap {TestDiagnostics.ByteDisplayCap})");

        var line = TestDiagnostics.FormatBytes("body", bytes);

        var hex = string.Join(' ', Enumerable.Repeat("61", TestDiagnostics.ByteDisplayCap));
        var text = new string('a', TestDiagnostics.ByteDisplayCap);
        Log.Act("line length", line.Length);
        Log.Diff("line", $"BYTES body (260 bytes): {hex} | {text} ... (4 more bytes)", line);
        Assert.AreEqual($"BYTES body (260 bytes): {hex} | {text} ... (4 more bytes)", line);
    }

    [TestMethod]
    public void FormatDiff_Bytes_FirstDifference_NamesIndexAndBothBytes()
    {
        Log.Arrange("expected and actual", "abc against ab\\n");

        var line = TestDiagnostics.FormatDiff("body", "abc"u8, "ab\n"u8);

        Log.Act("line", line);
        Log.Assert("line", "DIFF body: first difference at byte 2: expected 0x63 'c', actual 0x0a '.'", line);
        Assert.AreEqual("DIFF body: first difference at byte 2: expected 0x63 'c', actual 0x0a '.'", line);
    }

    [TestMethod]
    public void FormatDiff_Bytes_LengthsDiffer_NamesBothLengths()
    {
        Log.Arrange("expected and actual", "abc against ab; a against ab");

        var actualShorter = TestDiagnostics.FormatDiff("body", "abc"u8, "ab"u8);
        var actualLonger = TestDiagnostics.FormatDiff("body", "a"u8, "ab"u8);

        Log.Act("actual shorter", actualShorter);
        Log.Act("actual longer", actualLonger);
        Log.Assert("actual longer", "DIFF body: lengths differ, expected 1 bytes, actual 2 bytes, equal for the first 1", actualLonger);
        Assert.AreEqual("DIFF body: lengths differ, expected 3 bytes, actual 2 bytes, equal for the first 2", actualShorter);
        Assert.AreEqual("DIFF body: lengths differ, expected 1 bytes, actual 2 bytes, equal for the first 1", actualLonger);
    }

    [TestMethod]
    public void FormatDiff_Bytes_Equal_SaysEqual()
    {
        Log.Arrange("expected and actual", "abc against abc");

        var line = TestDiagnostics.FormatDiff("body", "abc"u8, "abc"u8);

        Log.Act("line", line);
        Log.Assert("line", "DIFF body: equal (3 bytes)", line);
        Assert.AreEqual("DIFF body: equal (3 bytes)", line);
    }

    [TestMethod]
    public void FormatDiff_Strings_FirstDifference_NamesCharacterIndex()
    {
        Log.Arrange("expected and actual", "abc against a\\tc");

        var line = TestDiagnostics.FormatDiff("stderr", "abc", "a\tc");

        Log.Act("line", line);
        Log.Assert("line", "DIFF stderr: first difference at character 1: expected 0x62 'b', actual 0x09 '.'", line);
        Assert.AreEqual("DIFF stderr: first difference at character 1: expected 0x62 'b', actual 0x09 '.'", line);
    }

    [TestMethod]
    public void FormatDiff_Strings_LengthsDifferAndEqual()
    {
        Log.Arrange("expected and actual", "ab against abc; ab against ab");

        var lengthsDiffer = TestDiagnostics.FormatDiff("stderr", "ab", "abc");
        var equal = TestDiagnostics.FormatDiff("stderr", "ab", "ab");

        Log.Act("lengths differ", lengthsDiffer);
        Log.Act("equal", equal);
        Log.Assert("equal", "DIFF stderr: equal (2 characters)", equal);
        Assert.AreEqual("DIFF stderr: lengths differ, expected 2 characters, actual 3 characters, equal for the first 2", lengthsDiffer);
        Assert.AreEqual("DIFF stderr: equal (2 characters)", equal);
    }

    [TestMethod]
    public void Phase_Disposed_WritesNameAndMilliseconds()
    {
        var (diagnostics, lines, clock) = Create();
        Log.Arrange("phase", "handshake, fake clock advance 42 ms inside it");

        using (diagnostics.Phase("handshake"))
        {
            clock.Advance(TimeSpan.FromMilliseconds(42));
        }

        Report(lines);
        Log.Assert("captured lines", "PHASE handshake: 42 ms", string.Join(" | ", lines));
        CollectionAssert.AreEqual(new[] { "PHASE handshake: 42 ms" }, lines);
    }

    [TestMethod]
    public void For_SameTestContext_ReturnsTheDiagnosticsTheGlobalHookStarted()
    {
        Log.Arrange("test context", TestContext.TestName ?? "(none)");

        var diagnostics = TestDiagnostics.For(TestContext);
        var again = TestDiagnostics.For(TestContext);

        diagnostics.Act("elapsed", diagnostics.Elapsed);
        diagnostics.Assert("same instance", true, ReferenceEquals(diagnostics, again));
        Assert.AreSame(diagnostics, again);
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
        foreach (var line in lines)
        {
            Log.Act("captured line", line);
        }
    }
}
