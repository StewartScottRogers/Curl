namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1587.c</c> as built without OpenSSL: upstream reads <c>CURLINFO_TLS_SESSION</c> and
/// <c>CURLINFO_TLS_SSL_PTR</c> and calls OpenSSL's <c>SSL_CTX_get_options</c> and <c>SSL_get_options</c> on them only when
/// libcurl uses OpenSSL (<c>USE_OPENSSL</c>), and otherwise <c>test()</c> does nothing and succeeds. Libcurl's TLS is
/// never OpenSSL, so this is the program as the Schannel build of curl ships it, as <see cref="Lib758"/> is: it ends at
/// once with success.
/// </summary>
public sealed class Lib1587 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1587";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        Task.FromResult(LibTestResult.Success);
}
