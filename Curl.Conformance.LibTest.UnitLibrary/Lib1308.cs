using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;
using Libcurl.Global;
using Libcurl.Mime;

namespace Curl.Conformance.LibTest;

#pragma warning disable CS0618 // lib1308 exercises the deprecated curl_formadd and curl_formget.

/// <summary>
/// <c>lib1308.c</c>: <c>curl_formadd</c> builds a three-field form (copied name and contents, a content type, pointed-to
/// contents), <c>curl_formget</c> writes it on stdout and must count 518 bytes; then a form with the file named
/// <c>argv[1]</c> as <c>custom named file</c> brings the running count to 899. Each failed check is written on
/// stderr and the program carries on; any makes it end with <see cref="LibTestErrorCodes.Failure"/>.
/// </summary>
public sealed class Lib1308 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1308";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library));

    /// <summary>The body after <c>global_init</c>: both forms, built, written and freed.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <returns>A task that completes at the program's cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        var check = new LibTestAssertions(context.StandardError);
        long totalSize = 0;
        long Append(ReadOnlySpan<byte> buffer)
        {
            context.StandardOutput.Write(buffer);
            totalSize += buffer.Length;
            return buffer.Length;
        }

        // Upstream also checks that post and last are one struct after the first add; Libcurl keeps no last pointer.
        WriteForm(check, library, Append, [
            [new(CurlFormOption.CopyName, "name"), new(CurlFormOption.CopyContents, "content")],
            [new(CurlFormOption.CopyName, "htmlcode"), new(CurlFormOption.CopyContents, "<HTML></HTML>"), new(CurlFormOption.ContentType, "text/html")],
            [new(CurlFormOption.CopyName, "name_for_ptrcontent"), new(CurlFormOption.PointerContents, "test buffer")],
        ]);
        _ = check.FailUnless(totalSize == 518, "curl_formget got wrong size back", "total_size == 518");
        WriteForm(check, library, Append, [
            [new(CurlFormOption.PointerName, "name of file field"), new(CurlFormOption.File, context.Url), new(CurlFormOption.FileName, "custom named file")],
        ]);
        _ = check.FailUnless(totalSize == 899, "curl_formget got wrong size back", "total_size == 899");
        steps.Result = check.Failures == 0 ? CurlCode.Ok : LibTestErrorCodes.Failure;
        return ValueTask.CompletedTask;
    }

    // curl_formadd for each field, curl_formget through append, curl_formfree.
    private static void WriteForm(LibTestAssertions check, CurlLibrary library, FormGetCallback append, CurlFormArgument[][] fields)
    {
        CurlForm? post = null;
        foreach (CurlFormArgument[] field in fields)
        {
            _ = check.FailUnless(CurlForm.Add(library, ref post, field) == CurlFormAddCode.Ok, "curl_formadd returned error", "rc == 0");
        }

        _ = check.FailUnless((post?.Get(append) ?? 0) == 0, "curl_formget returned error", "formres == 0");
        CurlForm.Free(post);
    }
}
