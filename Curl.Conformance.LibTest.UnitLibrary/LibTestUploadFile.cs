namespace Curl.Conformance.LibTest;

/// <summary>
/// The opening of <c>lib505.c</c> and <c>lib541.c</c>: the file <c>libtest_arg2</c> names is opened for upload
/// before <c>curl_global_init</c>. No second argument answers <see cref="LibTestErrorCodes.Usage"/>; a file that
/// cannot be opened or is empty answers <see cref="LibTestErrorCodes.MajorBad"/>. The <c>fopen()</c> line carries
/// the .NET exception's message in place of the C library's <c>errno</c> text, and the <c>fstat</c> check is left
/// out, as it cannot fail once the file is open.
/// </summary>
public static class LibTestUploadFile
{
    /// <summary>Opens the file and runs <paramref name="run"/> on it, closing it afterwards.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="run">The rest of the program, given the open file.</param>
    /// <returns>The exit code the program returns.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="run"/> is <see langword="null"/>.</exception>
    public static async Task<LibTestResult> RunWithFileAsync(LibTestContext context, Func<FileStream, Task<LibTestResult>> run)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(run);
        TextWriter error = context.StandardError;
        if (context.Argument2 is not { } path)
        {
            error.Write("Usage: <url> <file-to-upload>\n");
            return LibTestResult.FromCurlCode(LibTestErrorCodes.Usage);
        }

        FileStream source;
        try
        {
            source = File.OpenRead(path);
        }
        catch (IOException exception)
        {
            error.Write($"fopen() failed with error {exception.Message}\n");
            error.Write($"Error opening file '{path}'\n");
            return LibTestResult.FromCurlCode(LibTestErrorCodes.MajorBad);
        }

        await using (source.ConfigureAwait(false))
        {
            if (source.Length == 0)
            {
                error.Write($"File {path} has zero size!\n");
                return LibTestResult.FromCurlCode(LibTestErrorCodes.MajorBad);
            }

            return await run(source).ConfigureAwait(false);
        }
    }
}
