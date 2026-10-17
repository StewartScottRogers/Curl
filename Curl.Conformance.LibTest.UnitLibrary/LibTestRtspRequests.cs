using System.Runtime.CompilerServices;
using Libcurl.Easy;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// What the RTSP programs <c>lib568.c</c> to <c>lib572.c</c> repeat for each request: <c>stream_uri =
/// tutil_suburl(URL, request++)</c>, <see cref="LibTestErrorCodes.MajorBad"/> when there is no URL to extend, and
/// <c>easy_setopt(curl, CURLOPT_RTSP_STREAM_URI, stream_uri)</c>; and the upload file <c>lib568.c</c> and
/// <c>lib572.c</c> open from <c>libtest_arg2</c> after <c>curl_easy_init</c>.
/// </summary>
/// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
/// <param name="easy">The program's handle.</param>
/// <param name="baseUrl">The case's URL, which each request's number extends; <see langword="null"/> when there is none.</param>
public sealed class LibTestRtspRequests(LibTestSteps steps, CurlEasy easy, string? baseUrl)
{
    private readonly LibTestSteps steps = steps ?? throw new ArgumentNullException(nameof(steps));
    private readonly CurlEasy easy = easy ?? throw new ArgumentNullException(nameof(easy));
    private int request = 1;

    /// <summary>Sets <c>CURLOPT_RTSP_STREAM_URI</c> to the next request's URL, the case's URL and the request number in four digits.</summary>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the program goes on.</returns>
    public bool SetNextStreamUri([CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        if (baseUrl is null)
        {
            steps.Result = LibTestErrorCodes.MajorBad;
            return false;
        }

        return steps.EasySetOption(easy, CurlOption.RtspStreamUri, LibTestUtilities.SubUrl(baseUrl, request++), file, line);
    }

    /// <summary><see cref="SetNextStreamUri"/>, then <paramref name="options"/> and <c>result = curl_easy_perform(curl)</c>.</summary>
    /// <param name="options">The options set after the stream URI, in the program's order.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the program goes on: every option was set and the transfer succeeded.</returns>
    public async ValueTask<bool> PerformNextAsync(
        IEnumerable<(CurlOption Option, object? Value)> options,
        CancellationToken cancellationToken,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0) =>
        SetNextStreamUri(file, line) && await steps.SetOptionsAndPerformAsync(easy, options, cancellationToken, file, line).ConfigureAwait(false);

    /// <summary>
    /// The upload request of <c>lib568.c</c> and <c>lib572.c</c>: <see cref="SetNextStreamUri"/>, then opens the file
    /// <paramref name="path"/> names, sets <c>CURLOPT_RTSP_REQUEST</c> to <paramref name="request"/>,
    /// <c>CURLOPT_READDATA</c> to the file, <c>CURLOPT_UPLOAD</c> on and <c>CURLOPT_INFILESIZE_LARGE</c> to its size,
    /// then <paramref name="moreOptions"/>, performs, and on success turns <c>CURLOPT_UPLOAD</c> off; the file is closed
    /// either way. A file that cannot be opened writes <c>open() failed with error</c>, carrying the .NET exception's
    /// message in place of the C library's <c>errno</c> text, and <c>Error opening file '...'</c>, and
    /// <see cref="LibTestSteps.Result"/> becomes <see cref="LibTestErrorCodes.MajorBad"/>.
    /// </summary>
    /// <param name="path">The file, or <see langword="null"/> when the case gives none.</param>
    /// <param name="error">The program's <c>stderr</c>.</param>
    /// <param name="request">The RTSP request that uploads.</param>
    /// <param name="moreOptions">The options the program sets after the size.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>Whether the program goes on.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> or <paramref name="moreOptions"/> is <see langword="null"/>.</exception>
    public async ValueTask<bool> UploadAsync(
        string? path,
        TextWriter error,
        Curl.Api.Options.Protocols.RtspRequest request,
        IEnumerable<(CurlOption Option, object? Value)> moreOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(moreOptions);
        if (!SetNextStreamUri())
        {
            return false;
        }

        FileStream? source = OpenUpload(path, error);
        if (source is null)
        {
            return false;
        }

        await using (source.ConfigureAwait(false))
        {
            (CurlOption, object?)[] options =
            [
                (CurlOption.RtspRequest, (long)request),
                (CurlOption.Readdata, source),
                (CurlOption.Upload, 1L),
                (CurlOption.InfilesizeLarge, source.Length),
                .. moreOptions,
            ];
            if (!await steps.SetOptionsAndPerformAsync(easy, options, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            steps.EasySetOption(easy, CurlOption.Upload, 0L);
            return true;
        }
    }

    private FileStream? OpenUpload(string? path, TextWriter error)
    {
        try
        {
            return File.OpenRead(path!);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            error.Write($"open() failed with error {exception.Message}\n");
            error.Write($"Error opening file '{path}'\n");
            steps.Result = LibTestErrorCodes.MajorBad;
            return null;
        }
    }

    /// <summary>
    /// Opens <paramref name="path"/> for writing, as <c>curlx_fopen(libtest_arg2, "wb")</c> does before
    /// <c>curl_global_init</c> in <c>lib569.c</c> and <c>lib571.c</c>.
    /// </summary>
    /// <param name="path">The file, or <see langword="null"/> when the case gives none.</param>
    /// <returns>The open file, or <see langword="null"/> when it could not be created.</returns>
    public static FileStream? CreateOutput(string? path)
    {
        try
        {
            return File.Create(path!);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
