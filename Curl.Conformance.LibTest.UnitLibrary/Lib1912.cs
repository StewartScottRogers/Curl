namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1912.c</c>: the program as built without the GCC type-checking macros (<c>CURLINC_TYPECHECK_GCC_H</c> unset),
/// which has nothing to cross-check and succeeds at once. C# has no such macros: <c>curl_easy_setopt</c>'s
/// <c>object</c> value is checked by Libcurl's setter rows instead.
/// </summary>
public sealed class Lib1912 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1912";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        Task.FromResult(LibTestResult.Success);
}
