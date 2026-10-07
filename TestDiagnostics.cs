// Shared test diagnostics, linked into every .UnitTests project by Directory.Build.props
// (BL-1457, ADR-0416). Every test writes a START line before it runs and an END line
// after, with its outcome, elapsed milliseconds and how many ARRANGE, ACT and ASSERT or
// DIFF lines it wrote, and a SLOW: line when it ran over the 3-second budget. Tests add
// labelled context through TestDiagnostics.For(TestContext), so an AI reading a failed or
// slow test's log can debug it without re-running it. Documentation/Wiki/Test-Diagnostics.md
// describes each line.
//
// The type is internal so the copy linked into each assembly stays private to it. All
// per-test state lives on the per-test TestContext's Properties; nothing static is mutable,
// because MSTestSettings.cs runs tests in parallel at method level.
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Curl.Testing;

/// <summary>
/// Writes one test's diagnostic lines - START, ARRANGE, ACT, ASSERT, BYTES, DIFF, PHASE,
/// END and SLOW: - and counts the ARRANGE, ACT and ASSERT or DIFF lines for its END line.
/// </summary>
internal sealed class TestDiagnostics
{
    /// <summary>The longest a test may run, in milliseconds, before it writes a SLOW: line.</summary>
    public const int SlowTestBudgetMilliseconds = 3000;

    /// <summary>The most bytes a BYTES line shows before it writes how many more there are.</summary>
    public const int ByteDisplayCap = 256;

    private const string PropertyKey = "Curl.Testing.TestDiagnostics";

    private readonly Action<string> writeLine;

    private readonly TimeProvider timeProvider;

    private readonly long startTimestamp;

    /// <summary>Starts a test's diagnostics; its clock starts now.</summary>
    /// <param name="writeLine">Where each line goes, normally <see cref="TestContext.WriteLine(string?)" />.</param>
    /// <param name="timeProvider">The clock elapsed times are read from.</param>
    public TestDiagnostics(Action<string> writeLine, TimeProvider timeProvider)
    {
        this.writeLine = writeLine;
        this.timeProvider = timeProvider;
        startTimestamp = timeProvider.GetTimestamp();
    }

    /// <summary>Gets how many ARRANGE lines the test wrote.</summary>
    public int ArrangeCount { get; private set; }

    /// <summary>Gets how many ACT lines the test wrote.</summary>
    public int ActCount { get; private set; }

    /// <summary>Gets how many ASSERT and DIFF lines the test wrote.</summary>
    public int AssertCount { get; private set; }

    /// <summary>Gets the time since the diagnostics started.</summary>
    public TimeSpan Elapsed => timeProvider.GetElapsedTime(startTimestamp);

    /// <summary>Gets the running test's diagnostics, starting them if the global hook has not.</summary>
    /// <param name="testContext">The running test's context.</param>
    /// <returns>The diagnostics stored on <paramref name="testContext" />.</returns>
    public static TestDiagnostics For(TestContext testContext)
    {
        if (testContext.Properties.TryGetValue(PropertyKey, out var stored) && stored is TestDiagnostics diagnostics)
        {
            return diagnostics;
        }

        diagnostics = new TestDiagnostics(testContext.WriteLine, TimeProvider.System);
        testContext.Properties[PropertyKey] = diagnostics;
        return diagnostics;
    }

    /// <summary>Formats the START line.</summary>
    /// <param name="className">The test class's fully qualified name.</param>
    /// <param name="testName">The test method's name.</param>
    /// <param name="displayName">The display name, appended when it differs from <paramref name="testName" />.</param>
    /// <returns>The line.</returns>
    public static string FormatStart(string className, string testName, string? displayName) =>
        displayName is null || displayName == testName
            ? $"START {className}.{testName}"
            : $"START {className}.{testName} ({displayName})";

    /// <summary>Formats the END line.</summary>
    /// <param name="test">The test's full name.</param>
    /// <param name="outcome">The outcome MSTest reported.</param>
    /// <param name="elapsed">How long the test ran.</param>
    /// <param name="arrangeCount">How many ARRANGE lines it wrote.</param>
    /// <param name="actCount">How many ACT lines it wrote.</param>
    /// <param name="assertCount">How many ASSERT and DIFF lines it wrote.</param>
    /// <returns>The line.</returns>
    public static string FormatEnd(string test, string outcome, TimeSpan elapsed, int arrangeCount, int actCount, int assertCount) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"END {test}: {outcome} in {WholeMilliseconds(elapsed)} ms (arrange {arrangeCount}, act {actCount}, assert {assertCount})");

    /// <summary>Formats the SLOW: line, or returns null when the test ran within budget.</summary>
    /// <param name="test">The test's full name.</param>
    /// <param name="elapsed">How long the test ran.</param>
    /// <returns>The line, or null when <paramref name="elapsed" /> is at most the budget.</returns>
    public static string? FormatSlow(string test, TimeSpan elapsed)
    {
        var milliseconds = WholeMilliseconds(elapsed);
        return milliseconds > SlowTestBudgetMilliseconds
            ? string.Create(CultureInfo.InvariantCulture, $"SLOW: {test} took {milliseconds} ms (budget {SlowTestBudgetMilliseconds} ms)")
            : null;
    }

    /// <summary>Formats a BYTES line: hex pairs, then the bytes as text with non-printable bytes as '.'.</summary>
    /// <param name="label">What the bytes are.</param>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The line.</returns>
    public static string FormatBytes(string label, ReadOnlySpan<byte> bytes)
    {
        var shown = bytes.Length > ByteDisplayCap ? bytes[..ByteDisplayCap] : bytes;
        var hex = new StringBuilder(shown.Length * 3);
        var text = new StringBuilder(shown.Length);
        foreach (var value in shown)
        {
            hex.Append(hex.Length == 0 ? string.Empty : " ").Append(value.ToString("x2", CultureInfo.InvariantCulture));
            text.Append(Printable(value));
        }

        var more = bytes.Length > ByteDisplayCap
            ? string.Create(CultureInfo.InvariantCulture, $" ... ({bytes.Length - ByteDisplayCap} more bytes)")
            : string.Empty;
        return string.Create(CultureInfo.InvariantCulture, $"BYTES {label} ({bytes.Length} bytes): {hex} | {text}{more}");
    }

    /// <summary>Formats a DIFF line for two byte sequences.</summary>
    /// <param name="label">What is compared.</param>
    /// <param name="expected">The expected bytes.</param>
    /// <param name="actual">The actual bytes.</param>
    /// <returns>The line.</returns>
    public static string FormatDiff(string label, ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        var index = expected.CommonPrefixLength(actual);
        if (index < expected.Length && index < actual.Length)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"DIFF {label}: first difference at byte {index}: expected 0x{expected[index]:x2} '{Printable(expected[index])}', actual 0x{actual[index]:x2} '{Printable(actual[index])}'");
        }

        return FormatLengthDiff(label, "bytes", expected.Length, actual.Length);
    }

    /// <summary>Formats a DIFF line for two strings, by character index.</summary>
    /// <param name="label">What is compared.</param>
    /// <param name="expected">The expected text.</param>
    /// <param name="actual">The actual text.</param>
    /// <returns>The line.</returns>
    public static string FormatDiff(string label, string expected, string actual)
    {
        var index = expected.AsSpan().CommonPrefixLength(actual);
        if (index < expected.Length && index < actual.Length)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"DIFF {label}: first difference at character {index}: expected 0x{(int)expected[index]:x2} '{Printable(expected[index])}', actual 0x{(int)actual[index]:x2} '{Printable(actual[index])}'");
        }

        return FormatLengthDiff(label, "characters", expected.Length, actual.Length);
    }

    /// <summary>Writes an ARRANGE line: an input that matters.</summary>
    /// <param name="label">What the input is.</param>
    /// <param name="value">The input.</param>
    public void Arrange(string label, object? value)
    {
        ArrangeCount++;
        writeLine(string.Create(CultureInfo.InvariantCulture, $"ARRANGE {label}: {value}"));
    }

    /// <summary>Writes an ACT line: a result.</summary>
    /// <param name="label">What the result is.</param>
    /// <param name="value">The result.</param>
    public void Act(string label, object? value)
    {
        ActCount++;
        writeLine(string.Create(CultureInfo.InvariantCulture, $"ACT {label}: {value}"));
    }

    /// <summary>Writes an ASSERT line: what an assertion expects and what it got.</summary>
    /// <param name="label">What is asserted.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    public void Assert(string label, object? expected, object? actual)
    {
        AssertCount++;
        writeLine(string.Create(CultureInfo.InvariantCulture, $"ASSERT {label}: expected {expected}, actual {actual}"));
    }

    /// <summary>Writes a BYTES line.</summary>
    /// <param name="label">What the bytes are.</param>
    /// <param name="bytes">The bytes.</param>
    public void Bytes(string label, ReadOnlySpan<byte> bytes) => writeLine(FormatBytes(label, bytes));

    /// <summary>Writes a DIFF line for two byte sequences.</summary>
    /// <param name="label">What is compared.</param>
    /// <param name="expected">The expected bytes.</param>
    /// <param name="actual">The actual bytes.</param>
    public void Diff(string label, ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        AssertCount++;
        writeLine(FormatDiff(label, expected, actual));
    }

    /// <summary>Writes a DIFF line for two strings.</summary>
    /// <param name="label">What is compared.</param>
    /// <param name="expected">The expected text.</param>
    /// <param name="actual">The actual text.</param>
    public void Diff(string label, string expected, string actual)
    {
        AssertCount++;
        writeLine(FormatDiff(label, expected, actual));
    }

    /// <summary>Starts a phase; disposing the result writes its PHASE line.</summary>
    /// <param name="name">The phase's name.</param>
    /// <returns>The scope whose disposal ends the phase.</returns>
    public PhaseScope Phase(string name) => new(this, name, timeProvider.GetTimestamp());

    /// <summary>Writes the START line.</summary>
    /// <param name="className">The test class's fully qualified name.</param>
    /// <param name="testName">The test method's name.</param>
    /// <param name="displayName">The display name, appended when it differs from <paramref name="testName" />.</param>
    public void WriteStart(string className, string testName, string? displayName) =>
        writeLine(FormatStart(className, testName, displayName));

    /// <summary>Writes the END line, and the SLOW: line after it when the test ran over budget.</summary>
    /// <param name="test">The test's full name.</param>
    /// <param name="outcome">The outcome MSTest reported.</param>
    public void WriteEnd(string test, string outcome)
    {
        var elapsed = Elapsed;
        writeLine(FormatEnd(test, outcome, elapsed, ArrangeCount, ActCount, AssertCount));
        if (FormatSlow(test, elapsed) is { } slow)
        {
            writeLine(slow);
        }
    }

    private static long WholeMilliseconds(TimeSpan elapsed) => (long)elapsed.TotalMilliseconds;

    private static char Printable(byte value) => value is >= 0x20 and < 0x7f ? (char)value : '.';

    private static char Printable(char value) => value is >= ' ' and < (char)0x7f ? value : '.';

    private static string FormatLengthDiff(string label, string unit, int expectedLength, int actualLength) =>
        expectedLength == actualLength
            ? string.Create(CultureInfo.InvariantCulture, $"DIFF {label}: equal ({expectedLength} {unit})")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"DIFF {label}: lengths differ, expected {expectedLength} {unit}, actual {actualLength} {unit}, equal for the first {Math.Min(expectedLength, actualLength)}");

    /// <summary>A running phase; disposing it writes <c>PHASE &lt;name&gt;: &lt;n&gt; ms</c>.</summary>
    [SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "A disposable scope is never compared.")]
    internal readonly struct PhaseScope : IDisposable
    {
        private readonly TestDiagnostics owner;

        private readonly string name;

        private readonly long startTimestamp;

        internal PhaseScope(TestDiagnostics owner, string name, long startTimestamp)
        {
            this.owner = owner;
            this.name = name;
            this.startTimestamp = startTimestamp;
        }

        /// <summary>Writes the PHASE line.</summary>
        public void Dispose() =>
            owner.writeLine(string.Create(
                CultureInfo.InvariantCulture,
                $"PHASE {name}: {WholeMilliseconds(owner.timeProvider.GetElapsedTime(startTimestamp))} ms"));
    }
}

/// <summary>
/// MSTest's global test hooks: the START line before every test in the assembly, the END
/// line and, over budget, the SLOW: line after it. Internal, discovered through
/// <see cref="DiscoverInternalsAttribute" />, so the copy linked into each test assembly
/// never clashes with another's when one test project references another.
/// </summary>
[TestClass]
public sealed class TestDiagnosticsHooks
{
    /// <summary>Writes the START line before every test in the assembly.</summary>
    /// <param name="testContext">The test about to run.</param>
    [GlobalTestInitialize]
    public static void WriteStartLine(TestContext testContext) =>
        TestDiagnostics.For(testContext).WriteStart(
            testContext.FullyQualifiedTestClassName ?? string.Empty,
            testContext.TestName ?? string.Empty,
            testContext.TestDisplayName);

    /// <summary>Writes the END line, and the SLOW: line when over budget, after every test in the assembly.</summary>
    /// <param name="testContext">The test that ran.</param>
    [GlobalTestCleanup]
    public static void WriteEndLines(TestContext testContext) =>
        TestDiagnostics.For(testContext).WriteEnd(
            $"{testContext.FullyQualifiedTestClassName}.{testContext.TestName}",
            testContext.CurrentTestOutcome.ToString());
}
