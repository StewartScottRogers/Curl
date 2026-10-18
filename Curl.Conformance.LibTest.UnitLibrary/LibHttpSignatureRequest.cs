using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// The program <c>lib5000.c</c> and <c>lib5004.c</c> share: a verbose transfer of the case's URL signed by RFC 9421 HTTP
/// message signatures with the Ed25519 key <see cref="Key"/> as <see cref="KeyId"/>, its headers not on stdout, and
/// <c>libtest_arg2</c>, when given, as its one <c>CURLOPT_CONNECT_TO</c> entry. Each program adds its own options after
/// the key id (<see cref="ProgramOptions"/>). A failing <c>easy_setopt</c> ends the program with its code; otherwise the
/// transfer's code is the program's. A failing <c>curl_global_init</c> or <c>curl_easy_init</c> writes its failure and
/// ends with <see cref="LibTestErrorCodes.MajorBad"/>.
/// </summary>
/// <param name="programNumber">The program's number, such as 5000.</param>
public abstract class LibHttpSignatureRequest(int programNumber) : ILibTest
{
    /// <summary><c>CURLHTTPSIG_ED25519</c>: the <c>CURLOPT_HTTPSIG_ALGORITHM</c> value for Ed25519.</summary>
    public const long Ed25519 = 1;

    /// <summary>The Ed25519 private key the programs sign with, in hex.</summary>
    public const string Key = "9f8362f87a484a954e6e740c5b4c0e84229139a20aa8ab56ff66586f6a7d29c5";

    /// <summary>The key id the signatures carry.</summary>
    public const string KeyId = "test-key-ed25519";

    /// <inheritdoc/>
    public string ProgramName => $"lib{programNumber}";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            (CurlOption, object?)[] options =
            [
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Verbose, 1L),
                (CurlOption.HttpsigAlgorithm, Ed25519),
                (CurlOption.HttpsigKey, Key),
                (CurlOption.HttpsigKeyid, KeyId),
                .. ProgramOptions(),
                (CurlOption.Header, 0L),
                (CurlOption.Url, context.Url),
                (CurlOption.ConnectTo, context.Argument2 is { } connectTo ? CurlSlist.Append(null, connectTo) : null),
            ];
            _ = await steps.SetOptionsAndPerformAsync(easy, options, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The program's own options, set after <c>CURLOPT_HTTPSIG_KEYID</c>.</summary>
    /// <returns>The options, in the program's order.</returns>
    protected abstract IEnumerable<(CurlOption Option, object? Value)> ProgramOptions();
}
