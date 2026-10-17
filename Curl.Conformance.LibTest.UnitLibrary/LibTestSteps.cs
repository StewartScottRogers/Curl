using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// The step macros of curl 8.22.0's <c>tests/libtest/first.h</c> over <c>Libcurl</c>, carrying a
/// program's <c>result</c> variable as <see cref="Result"/>. Each step answers <see langword="true"/>
/// when the program goes on and <see langword="false"/> when it failed and <see cref="Result"/> holds
/// the code, where upstream's macro jumps to <c>test_cleanup</c> (or, for <c>global_init</c>,
/// returns); ignoring the answer is upstream's <c>res_*</c> form. A succeeding step never clears a
/// code set before it, except where upstream assigns the call's result outright
/// (<see cref="EasySetOption"/>, <see cref="GlobalInit"/>, <see cref="EasyPerformAsync"/> and
/// <see cref="EasyGetInfo"/>).
/// </summary>
/// <param name="error">Where a failing step writes the line upstream's macro writes to <c>stderr</c>.</param>
public sealed partial class LibTestSteps(TextWriter error)
{
    private readonly TextWriter error = error ?? throw new ArgumentNullException(nameof(error));

    /// <summary>The program's <c>result</c>: <see cref="CurlCode.Ok"/>, a libcurl code or a <see cref="LibTestErrorCodes"/> code.</summary>
    public CurlCode Result { get; set; }

    /// <summary>The exit code <c>main()</c> returns for <see cref="Result"/>.</summary>
    /// <returns>The result whose exit code is the number of <see cref="Result"/>.</returns>
    public LibTestResult ToLibTestResult() => LibTestResult.FromCurlCode(Result);

    /// <summary>
    /// <c>global_init(flags)</c>: <c>curl_global_init</c> over <paramref name="engine"/>. Libcurl's
    /// init fails only for want of an engine, so a <see langword="null"/> engine is the failing step:
    /// <see cref="Result"/> becomes <see cref="CurlCode.FailedInit"/>, as libcurl answers a failed init.
    /// </summary>
    /// <param name="engine">The engine the library's handles come from, or <see langword="null"/>.</param>
    /// <param name="flags">The <c>CURL_GLOBAL_*</c> flags.</param>
    /// <param name="library">The library, or <see langword="null"/> when the step failed.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the program goes on.</returns>
    public bool GlobalInit(
        ICurlEngine? engine,
        CurlGlobalFlags flags,
        [NotNullWhen(true)] out CurlLibrary? library,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        library = engine is null ? null : CurlGlobal.Init(engine, flags);
        Result = library is null ? CurlCode.FailedInit : CurlCode.Ok;
        return SucceededOrReported("curl_global_init", file, line);
    }

    /// <summary>
    /// <c>easy_init(handle)</c>: <c>curl_easy_init</c> on <paramref name="library"/>. A library
    /// already cleaned up gives no handle: <see cref="Result"/> becomes
    /// <see cref="LibTestErrorCodes.EasyInit"/>.
    /// </summary>
    /// <param name="library">The library to create the handle from.</param>
    /// <param name="easy">The handle, or <see langword="null"/> when the step failed.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the program goes on.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="library"/> is <see langword="null"/>.</exception>
    public bool EasyInit(
        CurlLibrary library,
        [NotNullWhen(true)] out CurlEasy? easy,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(library);
        easy = library.IsCleanedUp ? null : library.EasyInit();
        if (easy is not null)
        {
            return true;
        }

        Result = LibTestErrorCodes.EasyInit;
        WriteLine(file, line, "curl_easy_init() failed");
        return false;
    }

    /// <summary>
    /// <c>easy_setopt(handle, option, value)</c> (before 8.19, <c>test_setopt</c>): <see cref="Result"/>
    /// becomes <c>curl_easy_setopt</c>'s answer.
    /// </summary>
    /// <param name="easy">The handle.</param>
    /// <param name="option">The option to set.</param>
    /// <param name="value">The option's value.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the program goes on.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="easy"/> is <see langword="null"/>.</exception>
    public bool EasySetOption(
        CurlEasy easy,
        CurlOption option,
        object? value,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(easy);
        Result = easy.SetOption(option, value);
        return SucceededOrReported("curl_easy_setopt", file, line);
    }

    /// <summary>
    /// <c>result = curl_easy_perform(handle)</c>, the call every program makes in place of a macro:
    /// <see cref="Result"/> becomes the transfer's answer. Nothing is written, as upstream writes nothing.
    /// </summary>
    /// <param name="easy">The handle.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>Whether the program goes on.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="easy"/> is <see langword="null"/>.</exception>
    public async ValueTask<bool> EasyPerformAsync(CurlEasy easy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(easy);
        Result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
        return Result == CurlCode.Ok;
    }

    /// <summary>
    /// <c>result = curl_easy_getinfo(handle, info, &amp;value)</c>: <see cref="Result"/> becomes the
    /// call's answer. Nothing is written, as upstream writes nothing.
    /// </summary>
    /// <param name="easy">The handle.</param>
    /// <param name="info">The info to read.</param>
    /// <param name="value">The value, or <see langword="null"/> when the step failed.</param>
    /// <returns>Whether the program goes on.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="easy"/> is <see langword="null"/>.</exception>
    public bool EasyGetInfo(CurlEasy easy, CurlInfo info, out object? value)
    {
        ArgumentNullException.ThrowIfNull(easy);
        Result = easy.GetInfo(info, out value);
        return Result == CurlCode.Ok;
    }

    /// <summary>
    /// The programs' <c>curl_slist_append</c> check: <c>list = curl_slist_append(list, text)</c>, and
    /// when no list comes back, <c>curl_slist_append() failed</c> and <see cref="Result"/> becomes
    /// <see cref="LibTestErrorCodes.MajorBad"/>. Libcurl's append fails only for a
    /// <see langword="null"/> string, which is therefore the failing step; the list is then unchanged.
    /// </summary>
    /// <param name="list">The list to append to, or <see langword="null"/> to start one; the appended list on success.</param>
    /// <param name="text">The string to append, or <see langword="null"/>.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the program goes on.</returns>
    public bool SlistAppend(
        ref CurlSlist? list,
        string? text,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        if (text is not null)
        {
            list = CurlSlist.Append(list, text);
            return true;
        }

        Result = LibTestErrorCodes.MajorBad;
        WriteLine(file, line, "curl_slist_append() failed");
        return false;
    }

    private bool SucceededOrReported(string function, string file, int line)
    {
        if (Result == CurlCode.Ok)
        {
            return true;
        }

        WriteLine(file, line, $"{function}() failed, with code {(int)Result} ({CurlErrorText.Easy(Result)})");
        return false;
    }

    private void WriteLine(string file, int line, string message) =>
        error.Write($"{Path.GetFileName(file)}:{line} {message}\n");
}
