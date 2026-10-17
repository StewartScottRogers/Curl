namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1678.c</c> as built without <c>USE_SSLS_EXPORT</c>: upstream imports one SSL session packet 64 times with
/// <c>curl_easy_ssls_import</c> only when libcurl was built with session export, and otherwise <c>test()</c> does
/// nothing and succeeds. The Schannel build of curl, which Curl matches on Windows, has no <c>SSLS-EXPORT</c>, so this is
/// the program as that build ships it, as <see cref="Lib1587"/> is: it ends at once with success. The program over
/// <c>curl_easy_ssls_import</c>, for the OpenSSL build Curl matches elsewhere, waits on Libcurl having that call.
/// </summary>
public sealed class Lib1678 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1678";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        Task.FromResult(LibTestResult.Success);
}
