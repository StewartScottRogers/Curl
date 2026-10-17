using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1960.c</c>: hands libcurl a socket the application says is already connected - its
/// <c>CURLOPT_OPENSOCKETFUNCTION</c> gives <see cref="HandedOverSocket"/>, its <c>CURLOPT_SOCKOPTFUNCTION</c> answers
/// <see cref="SocketOptionResult.AlreadyConnected"/> and its <c>CURLOPT_CLOSESOCKETFUNCTION</c> leaves the socket
/// open - and makes a verbose GET of the case's URL with headers in the output; the perform's code is the program's.
/// The URL <c>check</c> (the case's precheck) answers <c>CURLE_OK</c> at once, writing nothing. <c>libtest_arg2</c> must
/// be an IPv4 address (<c>curlx_inet_pton() failed</c> on stderr otherwise) and <c>libtest_arg3</c> a port of at most
/// 65535 (silently, before <c>curl_global_init</c>, otherwise), each failure ending with
/// <see cref="LibTestErrorCodes.MajorBad"/>. Upstream connects an operating system socket to that address itself;
/// Libcurl's sockets are opaque numbers over the engine's connections (ADR-0610), never an operating system socket a
/// program could make, so the port checks the address and port as upstream does and hands over the number instead.
/// </summary>
public sealed class Lib1960 : ILibTest
{
    /// <summary>The URL whose run is the case's precheck.</summary>
    public const string CheckUrl = "check";

    /// <summary>The socket number the open-socket callback hands libcurl, standing for upstream's <c>client_fd</c>.</summary>
    public const int HandedOverSocket = 1960;

    /// <inheritdoc/>
    public string ProgramName => "lib1960";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Url == CheckUrl)
        {
            return Task.FromResult(LibTestResult.Success);
        }

        return IsPort(context.Argument3)
            ? LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken))
            : Task.FromResult(LibTestResult.FromCurlCode(LibTestErrorCodes.MajorBad));
    }

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
        steps.Result = LibTestErrorCodes.MajorBad;
        if (!IsIPv4Address(context.Argument2))
        {
            context.StandardError.Write("curlx_inet_pton() failed\n");
            return;
        }

        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            _ = await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Opensocketfunction, (OpenSocketCallback)((_, _) => new CurlSocket(HandedOverSocket))),
                    (CurlOption.Sockoptfunction, (SocketOptionCallback)((_, _) => SocketOptionResult.AlreadyConnected)),
                    (CurlOption.Closesocketfunction, (CloseSocketCallback)(_ => 0)),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Header, 1L),
                    (CurlOption.Url, context.Url),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary><c>curlx_str_number(&amp;libtest_arg3, &amp;port, 0xffff)</c>: whether the text is digits naming at most 65535.</summary>
    /// <param name="text">The text, or <see langword="null"/>.</param>
    /// <returns>Whether it is a port.</returns>
    public static bool IsPort(string? text) => ushort.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out _);

    /// <summary><c>curlx_inet_pton(AF_INET, ...)</c>: whether the text is a dotted-quad IPv4 address.</summary>
    /// <param name="text">The text, or <see langword="null"/>.</param>
    /// <returns>Whether it is one.</returns>
    public static bool IsIPv4Address(string? text) =>
        text is not null
        && text.Count(character => character == '.') == 3
        && IPAddress.TryParse(text, out IPAddress? address)
        && address.AddressFamily == AddressFamily.InterNetwork;
}
