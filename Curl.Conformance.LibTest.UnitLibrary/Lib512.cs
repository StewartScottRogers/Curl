using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib512.c</c>: a handle with verbose and headers on is duplicated (<c>curl_easy_duphandle</c>) and the
/// duplicate alone gets the URL and performs. The program answers its own code: 0 when the transfer
/// succeeds, 1 when it fails, 2 when setting the URL fails, 4 when <c>curl_easy_init</c> fails and 5 when
/// <c>curl_global_init</c> fails. Its 3, a failing <c>curl_easy_duphandle</c>, cannot happen here:
/// <see cref="CurlEasy.DupHandle"/> always answers a handle.
/// </summary>
public sealed class Lib512 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib512";

    /// <inheritdoc/>
    public async Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Engine is null)
        {
            return new LibTestResult(5);
        }

        CurlLibrary library = CurlGlobal.Init(context.Engine, CurlGlobalFlags.All);
        await using (library.ConfigureAwait(false))
        {
            return new LibTestResult(await TestAsync(context, library, cancellationToken).ConfigureAwait(false));
        }
    }

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>The program's code: 0, 1, 2 or, when <c>curl_easy_init</c> fails, 4.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask<int> TestAsync(LibTestContext context, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(library);
        if (library.IsCleanedUp)
        {
            return 4;
        }

        CurlEasy easy = library.EasyInit();
        easy.SetOption(CurlOption.Writedata, context.StandardOutput);
        easy.SetOption(CurlOption.Verbose, 1L);
        easy.SetOption(CurlOption.Header, 1L);
        CurlEasy duplicate = easy.DupHandle();
        int code = await PerformDuplicateAsync(duplicate, context.Url, cancellationToken).ConfigureAwait(false);
        await duplicate.DisposeAsync().ConfigureAwait(false);
        await easy.DisposeAsync().ConfigureAwait(false);
        return code;
    }

    /// <summary>Sets the duplicate's URL and, when that succeeds, performs it.</summary>
    /// <param name="duplicate">The duplicated handle.</param>
    /// <param name="url">The URL, of whatever type the caller hands <c>curl_easy_setopt</c>.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>0 when the transfer succeeds, 1 when it fails, 2 when the URL is refused.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="duplicate"/> is <see langword="null"/>.</exception>
    public static async ValueTask<int> PerformDuplicateAsync(CurlEasy duplicate, object? url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(duplicate);
        if (duplicate.SetOption(CurlOption.Url, url) != CurlCode.Ok)
        {
            return 2;
        }

        return await duplicate.PerformAsync(cancellationToken).ConfigureAwait(false) == CurlCode.Ok ? 0 : 1;
    }
}
