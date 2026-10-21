namespace Curl.Conformance.UnitProgram.Hashes;

/// <summary>
/// The checks of upstream's <c>tests/unit/unitcheck.h</c> that the hash, encoding and signing ports use:
/// a failed check writes one line, prefixed with the program's name, to standard error and adds one to
/// <c>unitfail</c>, the count <see cref="Result"/> returns.
/// </summary>
/// <param name="standardError">Where a failed check is written.</param>
/// <param name="programName">The program's name, such as <c>unit1302</c>, that prefixes each line.</param>
public sealed class UnitChecks(TextWriter standardError, string programName)
{
    private int failureCount;

    /// <summary>The program's result: how many checks failed so far.</summary>
    public UnitProgramResult Result => new(failureCount);

    /// <summary><c>verify_memory</c>: fails when <paramref name="actual"/> differs from <paramref name="expected"/>.</summary>
    /// <param name="actual">The bytes the Curl code produced.</param>
    /// <param name="expected">The bytes the C program expects.</param>
    /// <param name="what">What was compared, for the failure line.</param>
    public void VerifyMemory(ReadOnlySpan<byte> actual, ReadOnlySpan<byte> expected, string what) =>
        FailUnless(actual.SequenceEqual(expected), $"{what}: memory buffer FAILED match, got {Convert.ToHexStringLower(actual)} instead of {Convert.ToHexStringLower(expected)}");

    /// <summary><c>fail_unless</c>: fails, writing <paramref name="message"/>, when <paramref name="condition"/> is false.</summary>
    /// <param name="condition">What must hold.</param>
    /// <param name="message">The failure line.</param>
    public void FailUnless(bool condition, string message)
    {
        if (!condition)
        {
            standardError.WriteLine($"{programName}: {message}");
            failureCount++;
        }
    }

    /// <summary>
    /// Writes <paramref name="message"/> when <paramref name="condition"/> is false without counting a failure,
    /// as a C program does where it prints a difference but leaves <c>unitfail</c> alone.
    /// </summary>
    /// <param name="condition">What should hold.</param>
    /// <param name="message">The line written when it does not.</param>
    public void WarnUnless(bool condition, string message)
    {
        if (!condition)
        {
            standardError.WriteLine($"{programName}: {message}");
        }
    }
}
