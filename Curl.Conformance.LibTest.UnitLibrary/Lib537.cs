namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib537.c</c> as it builds without <c>getrlimit</c> and <c>setrlimit</c>: the base class library has
/// neither, so the program prints <c>system lacks necessary system function(s)</c> and answers
/// <see cref="LibTestErrorCodes.MajorBad"/>, which makes its case's precheck skip the case.
/// </summary>
public sealed class Lib537 : ILibTest
{
    /// <summary>What the program prints, with no newline.</summary>
    public const string LacksFunctionsMessage = "system lacks necessary system function(s)";

    /// <inheritdoc/>
    public string ProgramName => "lib537";

    /// <inheritdoc/>
    public async Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        StreamWriter output = LibTestRun.StandardOutputWriter(context);
        await using (output.ConfigureAwait(false))
        {
            await output.WriteAsync(LacksFunctionsMessage.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        return LibTestResult.FromCurlCode(LibTestErrorCodes.MajorBad);
    }
}
