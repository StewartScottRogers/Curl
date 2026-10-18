using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib2405.c</c>'s <c>test_run</c>: two handles (<see cref="Lib2405.SetEasy"/>) on one multi handle, multiplexed for
/// <see cref="Lib2405.UseHttp2Multiplex"/>, performed until nothing runs or a perform fails; each turn whose
/// <c>curl_multi_waitfds</c> gives descriptors runs <see cref="Lib2405.CountChecksPass"/> and polls those descriptors
/// on a second multi handle for <see cref="PollMilliseconds"/>. Every done message's code then becomes the result.
/// </summary>
public static class Lib2405Run
{
    /// <summary>The <c>curl_multi_poll</c> timeout of each turn.</summary>
    public const int PollMilliseconds = 500;

    /// <summary>Runs the two transfers.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="use">One of <see cref="Lib2405.UseHttp1"/>, <see cref="Lib2405.UseHttp2"/> and <see cref="Lib2405.UseHttp2Multiplex"/>.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>The most descriptors any turn's first <c>curl_multi_waitfds</c> gave (<c>max_fd_count</c>).</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask<int> RunAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, int use, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        var handles = new IAsyncDisposable?[4];
        int most = InitHandles(steps, library, handles)
            ? await TransferAsync(context, steps, [(CurlEasy)handles[0]!, (CurlEasy)handles[1]!], (CurlMulti)handles[2]!, (CurlMulti)handles[3]!, use, cancellationToken).ConfigureAwait(false)
            : 0;
        foreach (IAsyncDisposable? handle in handles)
        {
            await DisposeAsync(handle).ConfigureAwait(false);
        }

        return most;
    }

    private static ValueTask DisposeAsync(IAsyncDisposable? handle) => handle?.DisposeAsync() ?? ValueTask.CompletedTask;

    // Both easy handles, then both multi handles, each kept in handles as it is made.
    private static bool InitHandles(LibTestSteps steps, CurlLibrary library, IAsyncDisposable?[] handles)
    {
        if (!steps.EasyInit(library, out CurlEasy? first))
        {
            return false;
        }

        handles[0] = first;
        if (!steps.EasyInit(library, out CurlEasy? second))
        {
            return false;
        }

        handles[1] = second;
        if (!steps.MultiInit(library, out CurlMulti? multi))
        {
            return false;
        }

        handles[2] = multi;
        bool made = steps.MultiInit(library, out CurlMulti? pollMulti);
        handles[3] = pollMulti;
        return made;
    }

    /// <summary>The perform loop: each turn's checks and poll, until nothing runs, a perform fails or a check fails.</summary>
    /// <param name="error">Where a failing check writes why.</param>
    /// <param name="steps">The program's steps; a failing check sets <see cref="LibTestErrorCodes.Failure"/>.</param>
    /// <param name="multi">The multi handle running the transfers.</param>
    /// <param name="pollMulti">The second multi handle the descriptors are polled on.</param>
    /// <param name="cancellationToken">Stops the loop.</param>
    /// <returns>The most descriptors any turn gave.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask<int> LoopAsync(TextWriter error, LibTestSteps steps, CurlMulti multi, CurlMulti pollMulti, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(multi);
        ArgumentNullException.ThrowIfNull(pollMulti);
        var sockets = new CurlWaitSocket[Lib2405.WaitSocketCapacity];
        int most = 0;
        bool goesOn = true;
        while (goesOn)
        {
            var (performCode, running) = await multi.PerformAsync(cancellationToken).ConfigureAwait(false);
            if (running == 0 || performCode != CurlMultiCode.Ok)
            {
                return most;
            }

            (goesOn, most) = await TurnAsync(error, steps, multi, pollMulti, sockets, most, cancellationToken).ConfigureAwait(false);
        }

        return most;
    }

    // One turn after a perform: the waitfds calls, their checks and the poll on the second handle.
    private static async ValueTask<(bool GoesOn, int Most)> TurnAsync(TextWriter error, LibTestSteps steps, CurlMulti multi, CurlMulti pollMulti, CurlWaitSocket[] sockets, int most, CancellationToken cancellationToken)
    {
        CurlMultiCode code = multi.WaitFds(sockets, out int count);
        if (code != CurlMultiCode.Ok)
        {
            return (false, Fail(error, steps, $"curl_multi_waitfds() failed, code {(int)code}.\n", most));
        }

        if (count == 0)
        {
            return (true, most);
        }

        if (!Lib2405.CountChecksPass(error, steps, multi, count))
        {
            return (false, most);
        }

        most = Math.Max(most, count);
        (code, _) = await pollMulti.PollAsync(sockets.AsMemory(0, count), PollMilliseconds, cancellationToken).ConfigureAwait(false);
        return code == CurlMultiCode.Ok ? (true, most) : (false, Fail(error, steps, $"curl_multi_poll() failed, code {(int)code}.\n", most));
    }

    private static async ValueTask<int> TransferAsync(LibTestContext context, LibTestSteps steps, CurlEasy[] handles, CurlMulti multi, CurlMulti pollMulti, int use, CancellationToken cancellationToken)
    {
        foreach (CurlEasy easy in handles)
        {
            Lib2405.SetEasy(easy, context.Url, use);
        }

        if (use == Lib2405.UseHttp2Multiplex)
        {
            _ = multi.SetOption(CurlMultiOption.Pipelining, Lib2405.PipeMultiplex);
        }

        if (!steps.MultiAddHandle(multi, handles[0]) || !steps.MultiAddHandle(multi, handles[1]))
        {
            return 0;
        }

        int most = await LoopAsync(context.StandardError, steps, multi, pollMulti, cancellationToken).ConfigureAwait(false);
        ReadDoneResults(steps, multi);
        foreach (CurlEasy easy in handles)
        {
            _ = multi.RemoveHandle(easy);
        }

        return most;
    }

    private static int Fail(TextWriter error, LibTestSteps steps, string message, int most)
    {
        error.Write(message);
        steps.Result = LibTestErrorCodes.Failure;
        return most;
    }

    private static void ReadDoneResults(LibTestSteps steps, CurlMulti multi)
    {
        while (multi.InfoRead(out _) is { } message)
        {
            steps.Result = message.Type == CurlMultiMessageType.Done ? message.Result : steps.Result;
        }
    }
}
