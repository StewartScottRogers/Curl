using System.Globalization;
using Curl.Api.Codes;
using Libcurl.Global;
using Libcurl.Misc.ErrorText;
using Libcurl.Misc.Version;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib3026.c</c>: <see cref="ThreadCount"/> threads at once each make a <c>curl_global_init</c> and, when it
/// succeeds, a <c>curl_global_cleanup</c>; each failing one writes its line to stderr and the program ends with
/// <see cref="LibTestErrorCodes.MajorBad"/>. A library whose <c>curl_version_info</c> lacks <c>CURL_VERSION_THREADSAFE</c>
/// says so and ends the same way before any thread starts. Upstream's version check needs no
/// <c>curl_global_init</c>; Libcurl's <c>curl_version_info</c> takes a library, so the port makes one first, over the
/// case's engine.
/// </summary>
/// <param name="initAndCleanUp">One thread's <c>curl_global_init</c> and <c>curl_global_cleanup</c>, answering the init's code.</param>
public sealed class Lib3026(Func<ValueTask<CurlCode>> initAndCleanUp) : ILibTest
{
    /// <summary>Upstream's <c>NUM_THREADS</c>.</summary>
    public const int ThreadCount = 100;

    private readonly Func<ValueTask<CurlCode>> initAndCleanUp = initAndCleanUp ?? throw new ArgumentNullException(nameof(initAndCleanUp));

    /// <summary>Initializes the program with each thread's init and cleanup a real <see cref="CurlGlobal.Init(CurlGlobalFlags)"/>.</summary>
    public Lib3026()
        : this(InitAndCleanUpAsync)
    {
    }

    /// <inheritdoc/>
    public string ProgramName => "lib3026";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, initAndCleanUp));

    /// <summary>The program's body.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The library <c>curl_version_info</c> reads.</param>
    /// <param name="initAndCleanUp">One thread's init and cleanup.</param>
    /// <returns>A task that completes when every thread has ended.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, Func<ValueTask<CurlCode>> initAndCleanUp)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(initAndCleanUp);
        if ((CurlVersion.Info(library).Features & CurlVersionFeatures.ThreadSafe) == 0)
        {
            context.StandardError.Write("Lib3026.cs: Have threads but the CURL_VERSION_THREADSAFE feature flag is not set\n");
            steps.Result = LibTestErrorCodes.MajorBad;
            return;
        }

        CurlCode[] results = await Task.WhenAll(Enumerable.Range(0, ThreadCount).Select(_ => Task.Run(async () => await initAndCleanUp().ConfigureAwait(false)))).ConfigureAwait(false);
        for (int i = 0; i < results.Length; i++)
        {
            if (results[i] != CurlCode.Ok)
            {
                context.StandardError.Write(string.Create(CultureInfo.InvariantCulture, $"Lib3026.cs: thread[{i}]: curl_global_init() failed,with code {(int)results[i]} ({CurlErrorText.Easy(results[i])})\n"));
                steps.Result = LibTestErrorCodes.MajorBad;
            }
        }
    }

    /// <summary><c>t3026_run_thread</c>: a <c>curl_global_init</c> of its own and its <c>curl_global_cleanup</c>.</summary>
    /// <returns><see cref="CurlCode.Ok"/>: Libcurl's init gives a library or throws.</returns>
    public static async ValueTask<CurlCode> InitAndCleanUpAsync()
    {
        CurlLibrary library = CurlGlobal.Init(CurlGlobalFlags.All);
        await library.DisposeAsync().ConfigureAwait(false);
        return CurlCode.Ok;
    }
}
