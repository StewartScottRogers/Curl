using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// The checks the older programs (<c>lib500.c</c>, <c>lib501.c</c>, <c>lib505.c</c>, <c>lib508.c</c> and the
/// like) write out by hand rather than through a macro: <c>curl_global_init() failed</c> or
/// <c>curl_easy_init() failed</c> with no file or line, and <see cref="LibTestErrorCodes.MajorBad"/>; and a run
/// of <c>easy_setopt</c> calls that stops at the first one failing.
/// </summary>
public sealed partial class LibTestSteps
{
    /// <summary>
    /// <c>if(curl_global_init(flags) != CURLE_OK)</c>: <c>curl_global_init</c> over <paramref name="engine"/>.
    /// A <see langword="null"/> engine is the failing init: <c>curl_global_init() failed</c> and
    /// <see cref="Result"/> becomes <see cref="LibTestErrorCodes.MajorBad"/>.
    /// </summary>
    /// <param name="engine">The engine the library's handles come from, or <see langword="null"/>.</param>
    /// <param name="flags">The <c>CURL_GLOBAL_*</c> flags.</param>
    /// <param name="library">The library, or <see langword="null"/> when the step failed.</param>
    /// <returns>Whether the program goes on.</returns>
    public bool GlobalInitOrMajorBad(ICurlEngine? engine, CurlGlobalFlags flags, [NotNullWhen(true)] out CurlLibrary? library)
    {
        library = engine is null ? null : CurlGlobal.Init(engine, flags);
        return library is not null || HandleMissing("curl_global_init");
    }

    /// <summary>
    /// <c>curl = curl_easy_init(); if(!curl)</c> on <paramref name="library"/>. A library already cleaned up
    /// gives no handle: <c>curl_easy_init() failed</c> and <see cref="Result"/> becomes
    /// <see cref="LibTestErrorCodes.MajorBad"/>.
    /// </summary>
    /// <param name="library">The library to create the handle from.</param>
    /// <param name="easy">The handle, or <see langword="null"/> when the step failed.</param>
    /// <returns>Whether the program goes on.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="library"/> is <see langword="null"/>.</exception>
    public bool EasyInitOrMajorBad(CurlLibrary library, [NotNullWhen(true)] out CurlEasy? easy)
    {
        ArgumentNullException.ThrowIfNull(library);
        easy = library.IsCleanedUp ? null : library.EasyInit();
        return easy is not null || HandleMissing("curl_easy_init");
    }

    /// <summary>
    /// <c>multi = curl_multi_init(); if(!multi)</c> on <paramref name="library"/>. A library already cleaned up
    /// gives no handle: <c>curl_multi_init() failed</c> and <see cref="Result"/> becomes
    /// <see cref="LibTestErrorCodes.MajorBad"/>.
    /// </summary>
    /// <param name="library">The library to create the handle from.</param>
    /// <param name="multi">The handle, or <see langword="null"/> when the step failed.</param>
    /// <returns>Whether the program goes on.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="library"/> is <see langword="null"/>.</exception>
    public bool MultiInitOrMajorBad(CurlLibrary library, [NotNullWhen(true)] out CurlMulti? multi)
    {
        ArgumentNullException.ThrowIfNull(library);
        multi = library.IsCleanedUp ? null : library.MultiInit();
        return multi is not null || HandleMissing("curl_multi_init");
    }

    /// <summary>
    /// A run of <c>easy_setopt</c> steps (<see cref="EasySetOption"/>) in order, stopping at the first that
    /// fails, as upstream's macro jumps to <c>test_cleanup</c> there.
    /// </summary>
    /// <param name="easy">The handle.</param>
    /// <param name="options">The options and their values, in the program's order.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the program goes on: every option was set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="easy"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    public bool EasySetOptions(
        CurlEasy easy,
        IEnumerable<(CurlOption Option, object? Value)> options,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(easy);
        ArgumentNullException.ThrowIfNull(options);
        foreach (var (option, value) in options)
        {
            if (!EasySetOption(easy, option, value, file, line))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// <see cref="EasySetOptions"/>, then, when every option was set, <c>result = curl_easy_perform(curl)</c>
    /// (<see cref="EasyPerformAsync"/>).
    /// </summary>
    /// <param name="easy">The handle.</param>
    /// <param name="options">The options and their values, in the program's order.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the program goes on: every option was set and the transfer succeeded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="easy"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    public async ValueTask<bool> SetOptionsAndPerformAsync(
        CurlEasy easy,
        IEnumerable<(CurlOption Option, object? Value)> options,
        CancellationToken cancellationToken,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        if (!EasySetOptions(easy, options, file, line))
        {
            return false;
        }

        return await EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// <see cref="EasySetOptions"/>, then, when every option was set, <c>multi_add_handle(multi, easy)</c>
    /// (<see cref="MultiAddHandle"/>).
    /// </summary>
    /// <param name="easy">The handle.</param>
    /// <param name="options">The options and their values, in the program's order.</param>
    /// <param name="multi">The multi handle to add <paramref name="easy"/> to.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the program goes on: every option was set and the handle added.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public bool SetOptionsAndAddHandle(
        CurlEasy easy,
        IEnumerable<(CurlOption Option, object? Value)> options,
        Libcurl.Multi.CurlMulti multi,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(multi);
        return EasySetOptions(easy, options, file, line) && MultiAddHandle(multi, easy, file, line);
    }
}
