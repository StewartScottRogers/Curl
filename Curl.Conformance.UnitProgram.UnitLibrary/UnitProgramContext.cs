namespace Curl.Conformance.UnitProgram;

/// <summary>
/// What the unit test bundle's <c>main()</c> hands a unit program's <c>test()</c>: the case's number, the
/// words of its <c>&lt;client&gt;&lt;command&gt;</c> and, set by the case runner, its standard output and
/// standard error.
/// </summary>
public sealed class UnitProgramContext
{
    /// <summary>Creates the context of one run.</summary>
    /// <param name="testNumber">The case's number, <c>testnum</c>.</param>
    /// <param name="arguments">The arguments after the program's name, copied; possibly none.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="testNumber"/> is negative.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> or one of them is <see langword="null"/>.</exception>
    public UnitProgramContext(int testNumber, IReadOnlyList<string> arguments)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(testNumber);
        ArgumentNullException.ThrowIfNull(arguments);
        string[] copy = [.. arguments];
        if (Array.IndexOf(copy, null) >= 0)
        {
            throw new ArgumentNullException(nameof(arguments), "A unit program argument cannot be null.");
        }

        TestNumber = testNumber;
        Arguments = copy;
    }

    /// <summary>The case's number, <c>testnum</c>.</summary>
    public int TestNumber { get; }

    /// <summary>The arguments after the program's name.</summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>The program's standard output; nothing is kept when the runner sets none.</summary>
    public Stream StandardOutput { get; init; } = Stream.Null;

    /// <summary>The program's standard error, where <c>unitcheck.h</c> writes each failed check; nothing is kept when the runner sets none.</summary>
    public TextWriter StandardError { get; init; } = TextWriter.Null;
}
