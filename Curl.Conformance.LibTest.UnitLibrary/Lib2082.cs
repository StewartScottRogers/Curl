using System.Globalization;
using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib2082.c</c> (libprereq): a transfer of the case's URL whose <c>CURLOPT_PREREQFUNCTION</c> writes the connection's
/// addresses (bracketed when the URL holds <c>#ipv6</c>) and ports and answers <c>CURL_PREREQFUNC_ABORT</c> when the URL
/// holds <c>#err</c>, else <c>CURL_PREREQFUNC_OK</c>; the body goes to stderr, and <c>#redir</c> follows redirects. A
/// failing transfer writes <c>lib2082.c:&lt;line&gt; curl_easy_perform() failed with code N (text)</c>. The transfer's
/// code is the program's. The program does not check <c>curl_global_init</c>; a library that gives no handle ends with
/// <see cref="LibTestErrorCodes.MajorBad"/> and writes nothing.
/// </summary>
public sealed class Lib2082 : ILibTest
{
    /// <summary>The line upstream's failure message names.</summary>
    public const int PerformFailureLine = 91;

    /// <inheritdoc/>
    public string ProgramName => "lib2082";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(library);
        steps.Result = LibTestErrorCodes.MajorBad;
        CurlEasy? easy = library.IsCleanedUp ? null : library.EasyInit();
        if (easy is null)
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            using StreamWriter output = LibTestRun.StandardOutputWriter(context);
            SetOptions(context, easy, output);
            steps.Result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
            if (steps.Result != CurlCode.Ok)
            {
                context.StandardError.Write(string.Create(
                    CultureInfo.InvariantCulture,
                    $"lib2082.c:{PerformFailureLine} curl_easy_perform() failed with code {(int)steps.Result} ({CurlErrorText.Easy(steps.Result)})\n"));
            }
        }
    }

    /// <summary><c>prereq_callback</c>: writes the connection's addresses and ports and the answer, and answers it.</summary>
    /// <param name="output">The program's stdout.</param>
    /// <param name="ipv6">Whether the addresses are written in brackets.</param>
    /// <param name="answer">What the callback answers.</param>
    /// <param name="primaryIp">The server's address.</param>
    /// <param name="localIp">The local address.</param>
    /// <param name="primaryPort">The server's port.</param>
    /// <param name="localPort">The local port.</param>
    /// <returns><paramref name="answer"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public static PrerequestResult WritePrerequest(TextWriter output, bool ipv6, PrerequestResult answer, string primaryIp, string localIp, int primaryPort, int localPort)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.Write(ipv6 ? $"Connected to [{primaryIp}]\nConnected from [{localIp}]\n" : $"Connected to {primaryIp}\nConnected from {localIp}\n");
        output.Write(string.Create(CultureInfo.InvariantCulture, $"Remote port = {primaryPort}\nLocal port = {localPort}\nReturning = {(int)answer}\n"));
        return answer;
    }

    private static void SetOptions(LibTestContext context, CurlEasy easy, StreamWriter output)
    {
        string url = context.Url ?? string.Empty;
        bool ipv6 = url.Contains("#ipv6", StringComparison.Ordinal);
        PrerequestResult answer = url.Contains("#err", StringComparison.Ordinal) ? PrerequestResult.Abort : PrerequestResult.Ok;
        _ = easy.SetOption(CurlOption.Url, url);
        _ = easy.SetOption(CurlOption.Prereqfunction, (PrerequestCallback)((primaryIp, localIp, primaryPort, localPort) =>
            WritePrerequest(output, ipv6, answer, primaryIp, localIp, primaryPort, localPort)));
        _ = easy.SetOption(CurlOption.Writefunction, (WriteCallback)(data => WriteToError(context, data)));
        if (url.Contains("#redir", StringComparison.Ordinal))
        {
            _ = easy.SetOption(CurlOption.Followlocation, 1L);
        }
    }

    private static long WriteToError(LibTestContext context, ReadOnlySpan<byte> data)
    {
        context.StandardError.Write(Encoding.Latin1.GetString(data));
        return data.Length;
    }
}
