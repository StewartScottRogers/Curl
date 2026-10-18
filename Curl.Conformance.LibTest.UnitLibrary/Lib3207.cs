using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Share;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;
using Libcurl.Share;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib3207.c</c>: a share handle sharing the TLS session cache under per-data locks, and <see cref="ThreadCount"/>
/// threads at once, each making <see cref="TransfersPerThread"/> verbose transfers of the case's URL on a new handle
/// using the share, trusting <c>libtest_arg2</c>'s CA file, their bodies kept per thread. Once every thread is done,
/// each thread's bodies are written on stdout in thread order; a thread whose transfer failed writes nothing there,
/// and the last such thread's code is the program's. A failing perform writes <c>curl_easy_perform() failed: TEXT</c>
/// on stderr and ends its thread. A share handle that cannot be created writes <c>curl_share_init() failed</c> on
/// stderr and ends with <see cref="CurlCode.Ok"/>. The threads are tasks, each lock a semaphore.
/// </summary>
public sealed class Lib3207 : ILibTest
{
    /// <summary><c>THREAD_SIZE</c>: the threads the program runs at once.</summary>
    public const int ThreadCount = 16;

    /// <summary><c>PER_THREAD_SIZE</c>: the transfers each thread makes.</summary>
    public const int TransfersPerThread = 8;

    /// <inheritdoc/>
    public string ProgramName => "lib3207";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>, which upstream does not check.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(library);
        CurlShare? share = library.IsCleanedUp ? null : library.ShareInit();
        if (share is null)
        {
            context.StandardError.Write("curl_share_init() failed\n");
            return;
        }

        ThreadContext[] threads = [.. Enumerable.Range(0, ThreadCount).Select(_ => new ThreadContext())];
        await ExecuteAsync(context, library, share, threads, cancellationToken).ConfigureAwait(false);
        WriteBodies(context, steps, threads);
        _ = share.Cleanup();
    }

    private static void WriteBodies(LibTestContext context, LibTestSteps steps, ThreadContext[] threads)
    {
        foreach (ThreadContext thread in threads)
        {
            if (thread.Result != CurlCode.Ok)
            {
                steps.Result = thread.Result;
                continue;
            }

            foreach (byte[] body in thread.Contents)
            {
                context.StandardOutput.Write(body);
            }
        }
    }

    /// <summary>
    /// <c>test_thread</c>: up to <see cref="TransfersPerThread"/> transfers, each on a new handle using
    /// <paramref name="share"/>, their bodies appended to <paramref name="thread"/>; the first failure is written on
    /// stderr and ends the thread. A library that gives no handle skips the lap, as upstream's <c>if(curl)</c> does.
    /// </summary>
    /// <param name="context">The program's context.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="share">The share handle.</param>
    /// <param name="thread">Where the thread's bodies and code go.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the thread ends.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async Task RunThreadAsync(LibTestContext context, CurlLibrary library, CurlShare share, ThreadContext thread, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(thread);
        CurlCode result = CurlCode.Ok;
        for (int i = 0; i < TransfersPerThread && result == CurlCode.Ok; i++)
        {
            CurlEasy? easy = library.IsCleanedUp ? null : library.EasyInit();
            if (easy is null)
            {
                continue;
            }

            await using (easy.ConfigureAwait(false))
            {
                _ = easy.SetOption(CurlOption.Url, context.Url);
                _ = easy.SetOption(CurlOption.Share, share);
                _ = easy.SetOption(CurlOption.Cainfo, context.Argument2);
                _ = easy.SetOption(CurlOption.Writefunction, (WriteCallback)thread.Append);
                _ = easy.SetOption(CurlOption.Verbose, 1L);
                result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
            }

            WriteFailure(context, result);
        }

        thread.Result = result;
    }

    private static async Task ExecuteAsync(LibTestContext context, CurlLibrary library, CurlShare share, ThreadContext[] threads, CancellationToken cancellationToken)
    {
        SemaphoreSlim[] locks = [.. Enumerable.Range(0, (int)CurlLockData.Hsts + 1).Select(_ => new SemaphoreSlim(1, 1))];
        _ = share.SetOption(CurlShareOption.LockFunction, (ShareLockCallback)((_, data, _) => locks[(int)data].Wait(cancellationToken)));
        _ = share.SetOption(CurlShareOption.UnlockFunction, (ShareUnlockCallback)((_, data) => locks[(int)data].Release()));
        _ = share.SetOption(CurlShareOption.Share, CurlLockData.SslSession);
        await Task.WhenAll(threads.Select(thread => Task.Run(() => RunThreadAsync(context, library, share, thread, cancellationToken), cancellationToken))).ConfigureAwait(false);
        _ = share.SetOption(CurlShareOption.LockFunction, null);
        _ = share.SetOption(CurlShareOption.UnlockFunction, null);
        foreach (SemaphoreSlim semaphore in locks)
        {
            semaphore.Dispose();
        }
    }

    private static void WriteFailure(LibTestContext context, CurlCode result)
    {
        if (result == CurlCode.Ok)
        {
            return;
        }

        lock (context.StandardError)
        {
            context.StandardError.Write($"curl_easy_perform() failed: {CurlErrorText.Easy(result)}\n");
        }
    }

    /// <summary><c>struct Ctx</c>: one thread's bodies, in the order received, and its last transfer's code.</summary>
    public sealed class ThreadContext
    {
        private readonly List<byte[]> contents = [];

        /// <summary>The bodies' chunks, in the order received.</summary>
        public IReadOnlyList<byte[]> Contents => contents;

        /// <summary>The thread's code: <see cref="CurlCode.Ok"/>, or the failed transfer's.</summary>
        public CurlCode Result { get; set; }

        /// <summary><c>write_memory_callback</c>: keeps a copy of <paramref name="data"/>.</summary>
        /// <param name="data">The chunk received.</param>
        /// <returns>The chunk's length: all of it was taken.</returns>
        public long Append(ReadOnlySpan<byte> data)
        {
            contents.Add(data.ToArray());
            return data.Length;
        }
    }
}
