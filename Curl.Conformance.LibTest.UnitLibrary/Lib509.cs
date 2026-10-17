using Curl.Api.EngineContracts;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.Strings;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib509.c</c>: <c>curl_global_init_mem</c> with a <see cref="LibTestCountingAllocator"/>, a
/// <c>CURLOPT_USERAGENT</c> and a <c>curl_easy_escape</c> of 14 bytes, then <c>Callbacks were invoked!</c> on
/// <c>stdout</c> when any allocator function was called. A failed init writes
/// <c>curl_global_init_mem() failed</c> and answers <see cref="LibTestErrorCodes.MajorBad"/>. The setopt's
/// answer is not checked: Libcurl cannot refuse a user agent string.
/// </summary>
/// <param name="createAllocator">Makes the allocator of one run, upstream's <c>custom_*</c> functions and their <c>seen</c> count.</param>
public sealed class Lib509(Func<LibTestCountingAllocator> createAllocator) : ILibTest
{
    private static readonly byte[] EscapedBytes = [0x2f, 0x3a, 0x3b, 0x3c, 0x3d, 0x3e, 0x3f, 0x91, 0xa2, 0xb3, 0xc4, 0xd5, 0xe6, 0xf7];

    private readonly Func<LibTestCountingAllocator> createAllocator = createAllocator ?? throw new ArgumentNullException(nameof(createAllocator));

    /// <inheritdoc/>
    public string ProgramName => "lib509";

    /// <inheritdoc/>
    public async Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        LibTestCountingAllocator allocator = createAllocator();
        var steps = new LibTestSteps(context.StandardError);
        if (InitMemory(context.Engine, allocator) is not { } library)
        {
            context.StandardError.Write("curl_global_init_mem() failed\n");
            return LibTestResult.FromCurlCode(LibTestErrorCodes.MajorBad);
        }

        await using (library.ConfigureAwait(false))
        {
            await TestAsync(context, steps, library, allocator).ConfigureAwait(false);
        }

        return steps.ToLibTestResult();
    }

    /// <summary>The program's body after <c>curl_global_init_mem</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="allocator">The run's allocator, whose count decides the line.</param>
    /// <returns>A task that completes when the program reaches its cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, LibTestCountingAllocator allocator)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(allocator);
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            steps.EasySetOption(easy, CurlOption.Useragent, "test509");
            _ = CurlStrings.Escape(EscapedBytes);
            if (allocator.Seen > 0)
            {
                await using StreamWriter output = LibTestRun.StandardOutputWriter(context);
                await output.WriteLineAsync("Callbacks were invoked!").ConfigureAwait(false);
            }
        }
    }

    private static CurlLibrary? InitMemory(ICurlEngine? engine, LibTestCountingAllocator allocator) =>
        engine is null
            ? null
            : CurlGlobal.InitMemory(engine, CurlGlobalFlags.All, allocator.Malloc, allocator.Free, allocator.Realloc, allocator.Strdup, allocator.Calloc);
}
