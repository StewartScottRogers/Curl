namespace Curl.Conformance.UnitProgram;

/// <summary>
/// One ported upstream unit program (<c>tests/unit/unitNNNN.c</c> or <c>tests/tunit/toolNNNN.c</c> of
/// curl 8.22.0): the C# shape of its <c>test()</c> entry point, written over the Curl library that holds
/// what the C program tests (ADR-0711).
/// </summary>
/// <remarks>
/// An upstream case with the <c>unittest</c> feature runs the program its <c>&lt;client&gt;&lt;tool&gt;</c>
/// line names (<c>tool%TESTNUMBER</c>, <c>unit1305</c>), else <c>unit%TESTNUMBER</c>; the case runner finds
/// the port by <see cref="ProgramName"/> and compares <see cref="UnitProgramResult.ExitCode"/> with the case's
/// expected return code.
/// </remarks>
public interface IUnitProgram
{
    /// <summary>The program's name as a case names it once <c>%TESTNUMBER</c> is replaced, such as <c>unit1302</c> or <c>tool1394</c>.</summary>
    string ProgramName { get; }

    /// <summary>Runs the program, as the unit test bundle's <c>main()</c> runs its <c>test()</c>.</summary>
    /// <param name="context">The program's test number, arguments and output streams.</param>
    /// <param name="cancellationToken">Stops the program early.</param>
    /// <returns>How many of the program's checks failed, which is its exit code.</returns>
    Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken);
}
