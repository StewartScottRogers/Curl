namespace Curl.Conformance.UnitProgram;

/// <summary>
/// What a unit program's <c>test()</c> returns: <c>unitcheck.h</c>'s <c>unitfail</c>, the number of its
/// checks that failed, which is the process's exit code.
/// </summary>
/// <param name="FailureCount">How many checks failed; 0 when every one passed.</param>
public readonly record struct UnitProgramResult(int FailureCount)
{
    /// <summary>The result of a program whose every check passed.</summary>
    public static UnitProgramResult Success { get; } = new(0);

    /// <summary>The exit code the program's <c>main()</c> returns: <see cref="FailureCount"/>.</summary>
    public int ExitCode => FailureCount;
}
