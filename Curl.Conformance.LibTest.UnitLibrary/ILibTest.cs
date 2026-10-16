namespace Curl.Conformance.LibTest;

/// <summary>
/// One ported upstream libtest program (<c>tests/libtest/libNNN.c</c> of curl 8.22.0): the C#
/// shape of its <c>test()</c> entry point, written over <c>Libcurl</c>.
/// </summary>
/// <remarks>
/// An upstream case runs a program through its <c>&lt;client&gt;&lt;tool&gt;</c> line
/// (<c>lib%TESTNUMBER</c> or <c>libNNN</c>); the case runner finds the port by
/// <see cref="ProgramName"/>, builds its <see cref="LibTestContext"/> from the case's
/// <c>&lt;command&gt;</c> line and compares the <see cref="LibTestResult.ExitCode"/> with the
/// case's expected return code.
/// </remarks>
public interface ILibTest
{
    /// <summary>
    /// The program's name as a case's <c>&lt;tool&gt;</c> line names it once
    /// <c>%TESTNUMBER</c> is replaced, such as <c>lib500</c>.
    /// </summary>
    string ProgramName { get; }

    /// <summary>Runs the program, as libtest's <c>main()</c> runs its <c>test()</c>.</summary>
    /// <param name="context">The program's arguments, test number and URL.</param>
    /// <param name="cancellationToken">Stops the program early.</param>
    /// <returns>The exit code the program's <c>main()</c> would return.</returns>
    Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken);
}
