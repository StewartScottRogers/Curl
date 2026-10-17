namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib758.c</c> as built without OpenSSL 3: upstream compiles its socket-action transfer with a fake asynchronous
/// certificate check only when libcurl uses OpenSSL 3 or later (<c>T578_ENABLED</c>), since the check is an OpenSSL
/// <c>SSL_CTX</c> verify callback, and otherwise <c>test()</c> does nothing and succeeds. Libcurl's TLS is never
/// OpenSSL, so this is the program as the Schannel build of curl ships it: it ends at once with success.
/// </summary>
public sealed class Lib758 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib758";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        Task.FromResult(LibTestResult.Success);
}
