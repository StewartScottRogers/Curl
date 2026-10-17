using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Headers;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Headers;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1940.c</c> (cases 1940 to 1944 and 1946): a verbose GET of the case's URL following redirects, its body thrown
/// away, through the HTTP proxy <c>libtest_arg2</c> tunnelled when one is given; once it succeeds, each of
/// <see cref="HeaderNames"/> found with <c>curl_easy_header</c> is written on stdout - <c> name == value</c>, or
/// <c>- name == value (index/amount)</c> for every one of a name that came more than once - from the response
/// headers, the <c>CONNECT</c> response's when a proxy was given, the 1xx responses' and the trailers, in that order.
/// The request is the last one, or the first for case 1946. Then <see cref="CheckBadArguments"/>'s code, cast to a
/// <c>CURLcode</c> as upstream casts it, is the program's; a failing perform's code is the program's.
/// </summary>
public sealed class Lib1940 : ILibTest
{
    /// <summary>The case that reads the first request's headers rather than the last's.</summary>
    public const int FirstRequestCase = 1946;

    /// <summary><c>testdata</c>: the header names the program asks for, in its order and letter case.</summary>
    public static IReadOnlyList<string> HeaderNames { get; } =
        ["daTE", "Server", "content-type", "content-length", "location", "set-cookie", "silly-thing", "fold", "blank", "Blank2", "Test"];

    /// <inheritdoc/>
    public string ProgramName => "lib1940";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>global_init</c>.</summary>
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
        int request = context.TestNumber == FirstRequestCase ? 0 : ITransferHeaders.LastRequest;
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            bool viaProxy = !string.IsNullOrEmpty(context.Argument2);
            List<(CurlOption Option, object? Value)> options =
            [
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Url, context.Url),
                (CurlOption.Verbose, 1L),
                (CurlOption.Followlocation, 1L),
                (CurlOption.Writefunction, (WriteCallback)LibTestUtilities.ThrowawayWrite),
            ];
            if (viaProxy)
            {
                options.Add((CurlOption.Proxy, context.Argument2));
                options.Add((CurlOption.Httpproxytunnel, 1L));
            }

            if (!await steps.SetOptionsAndPerformAsync(easy, options, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            using (StreamWriter output = LibTestRun.StandardOutputWriter(context))
            {
                ShowHeaders(easy, request, CurlHeaderOrigin.Header, output);
                if (viaProxy)
                {
                    ShowHeaders(easy, request, CurlHeaderOrigin.Connect, output);
                }

                ShowHeaders(easy, request, CurlHeaderOrigin.Informational, output);
                ShowHeaders(easy, request, CurlHeaderOrigin.Trailer, output);
            }

            steps.Result = (CurlCode)(int)CheckBadArguments(easy, request);
        }
    }

    /// <summary><c>t1940_showem</c>: writes each of <see cref="HeaderNames"/> the request received from <paramref name="origin"/>.</summary>
    /// <param name="easy">The handle whose transfer is read.</param>
    /// <param name="request">The request, from 0, or <see cref="ITransferHeaders.LastRequest"/>.</param>
    /// <param name="origin">Where the headers came from.</param>
    /// <param name="output">The program's stdout.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public static void ShowHeaders(CurlEasy easy, int request, CurlHeaderOrigin origin, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);
        foreach (string name in HeaderNames)
        {
            if (easy.Header(name, 0, origin, request, out CurlHeader? header) != CurlHeaderCode.Ok)
            {
                continue;
            }

            if (header!.Amount > 1)
            {
                ShowEvery(easy, name, header, origin, request, output);
            }
            else
            {
                output.Write($" {header.Name} == {header.Value}\n");
            }
        }
    }

    /// <summary>
    /// <c>t1940_negative</c>: <c>curl_easy_header</c> with no handle, no name and no origin must answer
    /// <see cref="CurlHeaderCode.BadArgument"/>, <c>date</c> must be found, and <c>curl_easy_nextheader</c> with no
    /// previous header must give one. Upstream's check with no <c>hout</c> cannot be made, a C# <see langword="out"/>
    /// always being there, and its <c>curl_easy_nextheader</c> with no handle cannot fail, Libcurl answering
    /// <see langword="null"/> for one without asking the engine; both are left out.
    /// </summary>
    /// <param name="easy">The handle whose transfer is read.</param>
    /// <param name="request">The request, from 0, or <see cref="ITransferHeaders.LastRequest"/>.</param>
    /// <returns><see cref="CurlHeaderCode.Ok"/>, or the first answer that was not the expected one.</returns>
    public static CurlHeaderCode CheckBadArguments(CurlEasy easy, int request)
    {
        CurlHeaderCode[] badArgumentCalls =
        [
            CurlEasyHeaders.Header(null, "x", 0, CurlHeaderOrigin.Header, ITransferHeaders.LastRequest, out _),
            easy.Header(null, 0, CurlHeaderOrigin.Header, ITransferHeaders.LastRequest, out _),
            easy.Header("x", 0, CurlHeaderOrigin.None, ITransferHeaders.LastRequest, out _),
        ];
        foreach (CurlHeaderCode answer in badArgumentCalls)
        {
            if (answer != CurlHeaderCode.BadArgument)
            {
                return answer;
            }
        }

        CurlHeaderCode code = easy.Header("date", 0, CurlHeaderOrigin.Header, request, out CurlHeader? date);
        if (code != CurlHeaderCode.Ok)
        {
            return code;
        }

        if (date is null)
        {
            return CurlHeaderCode.NoHeaders;
        }

        return easy.NextHeader(CurlHeaderOrigin.Header, ITransferHeaders.LastRequest, null) is null ? CurlHeaderCode.BadIndex : CurlHeaderCode.Ok;
    }

    // Writes every header of the name, "- name == value (index/amount)", stopping at the first that cannot be read.
    private static void ShowEvery(CurlEasy easy, string name, CurlHeader first, CurlHeaderOrigin origin, int request, TextWriter output)
    {
        int amount = first.Amount;
        CurlHeader? header = first;
        int index = 0;
        do
        {
            output.Write($"- {header!.Name} == {header.Value} ({index}/{amount})\n");
            index++;
        }
        while (index != amount && easy.Header(name, index, origin, request, out header) == CurlHeaderCode.Ok);
    }
}
