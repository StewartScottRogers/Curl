using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.ErrorText;
using Libcurl.Multi;

namespace Curl.Conformance.LibTest;

/// <summary>
/// The <c>multi_*</c> step macros of curl 8.22.0's <c>tests/libtest/first.h</c> over <c>Libcurl</c>'s
/// <see cref="CurlMulti"/>. A failing <c>curl_multi_*</c> call writes
/// <c>&lt;file&gt;:&lt;line&gt; curl_multi_X() failed, with code N (text)</c> and sets
/// <see cref="Result"/> to <see cref="LibTestErrorCodes.Multi"/> (<see cref="LibTestErrorCodes.BadTimeout"/>
/// for <c>multi_timeout</c>); a call that succeeds with an impossible count writes
/// <c>... succeeded, but returned invalid X value (N)</c> and sets <see cref="LibTestErrorCodes.NumHandles"/>
/// (<see cref="LibTestErrorCodes.BadTimeout"/> for <c>multi_timeout</c>). A succeeding step leaves
/// <see cref="Result"/> as it was.
/// </summary>
public sealed partial class LibTestSteps
{
    /// <summary>
    /// <c>multi_init(handle)</c>: <c>curl_multi_init</c> on <paramref name="library"/>. A library
    /// already cleaned up gives no handle: <c>curl_multi_init() failed</c> and <see cref="Result"/>
    /// becomes <see cref="LibTestErrorCodes.Multi"/>.
    /// </summary>
    /// <param name="library">The library to create the handle from.</param>
    /// <param name="multi">The handle, or <see langword="null"/> when the step failed.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the program goes on.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="library"/> is <see langword="null"/>.</exception>
    public bool MultiInit(
        CurlLibrary library,
        [NotNullWhen(true)] out CurlMulti? multi,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(library);
        multi = library.IsCleanedUp ? null : library.MultiInit();
        if (multi is not null)
        {
            return true;
        }

        Result = LibTestErrorCodes.Multi;
        WriteLine(file, line, "curl_multi_init() failed");
        return false;
    }

    /// <summary><c>multi_setopt(handle, option, value)</c>: <c>curl_multi_setopt</c>.</summary>
    /// <param name="multi">The handle.</param>
    /// <param name="option">The option to set.</param>
    /// <param name="value">The option's value.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the program goes on.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="multi"/> is <see langword="null"/>.</exception>
    public bool MultiSetOption(
        CurlMulti multi,
        CurlMultiOption option,
        object? value,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(multi);
        return MultiSucceededOrReported(multi.SetOption(option, value), "curl_multi_setopt", LibTestErrorCodes.Multi, file, line);
    }

    /// <summary><c>multi_add_handle(multi, easy)</c>: <c>curl_multi_add_handle</c>.</summary>
    /// <param name="multi">The multi handle.</param>
    /// <param name="easy">The easy handle to add.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the program goes on.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="multi"/> is <see langword="null"/>.</exception>
    public bool MultiAddHandle(
        CurlMulti multi,
        CurlEasy? easy,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(multi);
        return MultiSucceededOrReported(multi.AddHandle(easy), "curl_multi_add_handle", LibTestErrorCodes.Multi, file, line);
    }

    /// <summary><c>multi_remove_handle(multi, easy)</c>: <c>curl_multi_remove_handle</c>.</summary>
    /// <param name="multi">The multi handle.</param>
    /// <param name="easy">The easy handle to remove.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the program goes on.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="multi"/> is <see langword="null"/>.</exception>
    public bool MultiRemoveHandle(
        CurlMulti multi,
        CurlEasy? easy,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(multi);
        return MultiSucceededOrReported(multi.RemoveHandle(easy), "curl_multi_remove_handle", LibTestErrorCodes.Multi, file, line);
    }

    /// <summary>
    /// <c>multi_perform(multi, &amp;running)</c>: <c>curl_multi_perform</c>; a negative running count
    /// fails with <see cref="LibTestErrorCodes.NumHandles"/>.
    /// </summary>
    /// <param name="multi">The handle.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the program goes on, and the running count the call gave.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="multi"/> is <see langword="null"/>.</exception>
    public async ValueTask<(bool GoesOn, int RunningHandles)> MultiPerformAsync(
        CurlMulti multi,
        CancellationToken cancellationToken,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(multi);
        var (code, running) = await multi.PerformAsync(cancellationToken).ConfigureAwait(false);
        bool goesOn = MultiSucceededOrReported(code, "curl_multi_perform", LibTestErrorCodes.Multi, file, line)
            && CountValidOrReported(running, 0, "curl_multi_perform", "running_handles", LibTestErrorCodes.NumHandles, file, line);
        return (goesOn, running);
    }

    /// <summary>
    /// <c>multi_fdset(multi, &amp;read, &amp;write, &amp;exception, &amp;max_fd)</c>: <c>curl_multi_fdset</c>;
    /// a <paramref name="maxSocket"/> below -1 fails with <see cref="LibTestErrorCodes.NumHandles"/>.
    /// </summary>
    /// <param name="multi">The handle.</param>
    /// <param name="readSockets">Gets the sockets waiting to read, or <see langword="null"/>.</param>
    /// <param name="writeSockets">Gets the sockets waiting to write, or <see langword="null"/>.</param>
    /// <param name="exceptionSockets">Gets the sockets waiting for priority data, or <see langword="null"/>.</param>
    /// <param name="maxSocket">The highest socket, or -1 for none.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the program goes on.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="multi"/> is <see langword="null"/>.</exception>
    public bool MultiFdSet(
        CurlMulti multi,
        ISet<CurlSocket>? readSockets,
        ISet<CurlSocket>? writeSockets,
        ISet<CurlSocket>? exceptionSockets,
        out nint maxSocket,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(multi);
        var code = multi.FdSet(readSockets, writeSockets, exceptionSockets, out maxSocket);
        return MultiSucceededOrReported(code, "curl_multi_fdset", LibTestErrorCodes.Multi, file, line)
            && CountValidOrReported(maxSocket, -1, "curl_multi_fdset", "max_fd", LibTestErrorCodes.NumHandles, file, line);
    }

    /// <summary>
    /// <c>multi_timeout(multi, &amp;timeout)</c>: <c>curl_multi_timeout</c>; a failure or a timeout
    /// below -1 sets <see cref="LibTestErrorCodes.BadTimeout"/>.
    /// </summary>
    /// <param name="multi">The handle.</param>
    /// <param name="timeoutMilliseconds">The timeout the call gave, -1 for none.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the program goes on.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="multi"/> is <see langword="null"/>.</exception>
    public bool MultiTimeout(
        CurlMulti multi,
        out long timeoutMilliseconds,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(multi);
        (var code, timeoutMilliseconds) = multi.Timeout();
        return MultiSucceededOrReported(code, "curl_multi_timeout", LibTestErrorCodes.BadTimeout, file, line)
            && CountValidOrReported(timeoutMilliseconds, -1, "curl_multi_timeout", "timeout", LibTestErrorCodes.BadTimeout, file, line);
    }

    /// <summary>
    /// <c>multi_poll(multi, extra, count, timeout, &amp;numfds)</c>: <c>curl_multi_poll</c>; a negative
    /// ready count fails with <see cref="LibTestErrorCodes.NumHandles"/>.
    /// </summary>
    /// <param name="multi">The handle.</param>
    /// <param name="extraSockets">Extra sockets to wait on.</param>
    /// <param name="timeoutMilliseconds">The longest wait.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the program goes on, and the ready count (<c>numfds</c>) the call gave.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="multi"/> is <see langword="null"/>.</exception>
    public async ValueTask<(bool GoesOn, int ReadySockets)> MultiPollAsync(
        CurlMulti multi,
        Memory<CurlWaitSocket> extraSockets,
        int timeoutMilliseconds,
        CancellationToken cancellationToken,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(multi);
        var (code, ready) = await multi.PollAsync(extraSockets, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
        return (ReadyCountValidOrReported(code, ready, "curl_multi_poll", file, line), ready);
    }

    /// <summary>
    /// The programs' <c>curl_multi_wait(multi, extra, count, timeout, &amp;numfds)</c> check, held to
    /// <c>multi_poll</c>'s rules (8.22.0 has no <c>multi_wait</c> macro): a failure sets
    /// <see cref="LibTestErrorCodes.Multi"/>, a negative ready count <see cref="LibTestErrorCodes.NumHandles"/>.
    /// </summary>
    /// <param name="multi">The handle.</param>
    /// <param name="extraSockets">Extra sockets to wait on.</param>
    /// <param name="timeoutMilliseconds">The longest wait.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the program goes on, and the ready count (<c>numfds</c>) the call gave.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="multi"/> is <see langword="null"/>.</exception>
    public async ValueTask<(bool GoesOn, int ReadySockets)> MultiWaitAsync(
        CurlMulti multi,
        Memory<CurlWaitSocket> extraSockets,
        int timeoutMilliseconds,
        CancellationToken cancellationToken,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(multi);
        var (code, ready) = await multi.WaitAsync(extraSockets, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
        return (ReadyCountValidOrReported(code, ready, "curl_multi_wait", file, line), ready);
    }

    /// <summary><c>res_multi_wakeup(multi)</c>: <c>curl_multi_wakeup</c>.</summary>
    /// <param name="multi">The handle.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the call succeeded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="multi"/> is <see langword="null"/>.</exception>
    public bool MultiWakeup(
        CurlMulti multi,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(multi);
        return MultiSucceededOrReported(multi.Wakeup(), "curl_multi_wakeup", LibTestErrorCodes.Multi, file, line);
    }

    private bool ReadyCountValidOrReported(CurlMultiCode code, int ready, string function, string file, int line) =>
        MultiSucceededOrReported(code, function, LibTestErrorCodes.Multi, file, line)
        && CountValidOrReported(ready, 0, function, "numfds", LibTestErrorCodes.NumHandles, file, line);

    private bool MultiSucceededOrReported(CurlMultiCode code, string function, CurlCode failure, string file, int line)
    {
        if (code == CurlMultiCode.Ok)
        {
            return true;
        }

        Result = failure;
        WriteLine(file, line, $"{function}() failed, with code {(int)code} ({CurlErrorText.Multi(code)})");
        return false;
    }

    private bool CountValidOrReported(long value, long minimum, string function, string name, CurlCode failure, string file, int line)
    {
        if (value >= minimum)
        {
            return true;
        }

        Result = failure;
        WriteLine(file, line, $"{function}() succeeded, but returned invalid {name} value ({value})");
        return false;
    }
}
