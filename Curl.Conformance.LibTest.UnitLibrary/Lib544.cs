using System.Text;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib544.c</c>: a verbose POST with headers on, its body set by <c>CURLOPT_COPYPOSTFIELDS</c> from a buffer
/// with a NUL inside, which is then overwritten with <c>FAIL</c> to show the body was copied; the handle is
/// duplicated with <c>curl_easy_duphandle</c>, the original cleaned up and the duplicate performed. Case 545 sets
/// <c>CURLOPT_POSTFIELDSIZE</c> to the whole buffer first; otherwise the body is the C string up to the NUL,
/// <c>This</c>, as libcurl reads a <c>char *</c> with no size.
/// </summary>
public sealed class Lib544 : ILibTest
{
    /// <summary>The bytes of the buffer, a NUL after <c>This</c>.</summary>
    public static ReadOnlyMemory<byte> TestString { get; } = Encoding.ASCII.GetBytes("This\0 is test binary data with an embedded NUL");

    /// <inheritdoc/>
    public string ProgramName => "lib544";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches its cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        byte[] testString = TestString.ToArray();
        bool withSize = context.TestNumber == 545;
        List<(CurlOption Option, object? Value)> options = [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Url, context.Url)];
        if (withSize)
        {
            options.Add((CurlOption.Postfieldsize, (long)testString.Length));
        }

        options.Add((CurlOption.Copypostfields, withSize ? testString : testString[..Array.IndexOf(testString, (byte)0)]));
        options.Add((CurlOption.Verbose, 1L));
        options.Add((CurlOption.Header, 1L));
        await SetDuplicateAndPerformAsync(steps, easy, options, () => "FAIL\0"u8.CopyTo(testString), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets <paramref name="options"/> on <paramref name="easy"/>, then, when every one was set, runs
    /// <paramref name="beforeDuplicate"/>, duplicates the handle, cleans up the original and performs the
    /// duplicate. Either way the original is cleaned up.
    /// </summary>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="easy">The handle to set and duplicate; this method cleans it up.</param>
    /// <param name="options">The options, in the program's order.</param>
    /// <param name="beforeDuplicate">What the program does between the options and the duplicate.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches its cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask SetDuplicateAndPerformAsync(
        LibTestSteps steps,
        CurlEasy easy,
        IEnumerable<(CurlOption Option, object? Value)> options,
        Action beforeDuplicate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(easy);
        ArgumentNullException.ThrowIfNull(beforeDuplicate);
        if (!steps.EasySetOptions(easy, options))
        {
            await easy.DisposeAsync().ConfigureAwait(false);
            return;
        }

        beforeDuplicate();
        CurlEasy duplicate = easy.DupHandle();
        await easy.DisposeAsync().ConfigureAwait(false);
        await using (duplicate.ConfigureAwait(false))
        {
            await steps.EasyPerformAsync(duplicate, cancellationToken).ConfigureAwait(false);
        }
    }
}
