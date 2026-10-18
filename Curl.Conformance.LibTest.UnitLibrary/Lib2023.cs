using System.Globalization;
using Curl.Api.Options.Authentication;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib2023.c</c> (libauthretry): <c>libtest_arg2</c> names the main scheme and <c>libtest_arg3</c> the fallback
/// (<c>basic</c>, <c>digest</c> or <c>ntlm</c>, any case). On one handle, request 100 with the wrong password under the
/// main scheme, then request 200 with the right one under the fallback; on a fresh handle, requests 300 and 400 with the
/// wrong password under the main and fallback schemes, then request 500 with the right one under the fallback. Each request
/// is the case's URL with its number in four digits, verbose, with its headers in the body, and stops the program when it
/// fails. A scheme not named writes <c>auth schemes not found on commandline</c> and ends with
/// <see cref="LibTestErrorCodes.MajorBad"/> before <c>curl_global_init</c>; a failing <c>curl_global_init</c> or
/// <c>curl_easy_init</c> writes its failure and ends with <see cref="LibTestErrorCodes.MajorBad"/>.
/// </summary>
public sealed class Lib2023 : ILibTest
{
    /// <summary>The wrong credentials.</summary>
    public const string WrongPassword = "testuser:wrongpass";

    /// <summary>The right credentials.</summary>
    public const string RightPassword = "testuser:testpass";

    /// <inheritdoc/>
    public string ProgramName => "lib2023";

    /// <summary><c>parse_auth_name</c>: the scheme <paramref name="name"/> names, or <see cref="HttpAuthenticationMethods.None"/>.</summary>
    /// <param name="name">The scheme's name, or <see langword="null"/>.</param>
    /// <returns>The scheme.</returns>
    public static HttpAuthenticationMethods ParseAuthName(string? name) => name?.ToUpperInvariant() switch
    {
        "BASIC" => HttpAuthenticationMethods.Basic,
        "DIGEST" => HttpAuthenticationMethods.Digest,
        "NTLM" => HttpAuthenticationMethods.Ntlm,
        _ => HttpAuthenticationMethods.None,
    };

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        HttpAuthenticationMethods main = ParseAuthName(context.Argument2);
        HttpAuthenticationMethods fallback = ParseAuthName(context.Argument3);
        if (main == HttpAuthenticationMethods.None || fallback == HttpAuthenticationMethods.None)
        {
            context.StandardError.Write("auth schemes not found on commandline\n");
            return Task.FromResult(LibTestResult.FromCurlCode(LibTestErrorCodes.MajorBad));
        }

        return LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, main, fallback, cancellationToken));
    }

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="main">The main scheme.</param>
    /// <param name="fallback">The fallback scheme.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(
        LibTestContext context,
        LibTestSteps steps,
        CurlLibrary library,
        HttpAuthenticationMethods main,
        HttpAuthenticationMethods fallback,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (await RequestsOnOneHandleAsync(context, steps, library, [(100, main, WrongPassword), (200, fallback, RightPassword)], cancellationToken).ConfigureAwait(false))
        {
            _ = await RequestsOnOneHandleAsync(
                context,
                steps,
                library,
                [(300, main, WrongPassword), (400, fallback, WrongPassword), (500, fallback, RightPassword)],
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask<bool> RequestsOnOneHandleAsync(
        LibTestContext context,
        LibTestSteps steps,
        CurlLibrary library,
        (int Sequence, HttpAuthenticationMethods Scheme, string UserPassword)[] requests,
        CancellationToken cancellationToken)
    {
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return false;
        }

        await using (easy.ConfigureAwait(false))
        {
            _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
            foreach (var (sequence, scheme, userPassword) in requests)
            {
                if (!await SendRequestAsync(context, steps, easy, sequence, scheme, userPassword, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private static ValueTask<bool> SendRequestAsync(
        LibTestContext context,
        LibTestSteps steps,
        CurlEasy easy,
        int sequence,
        HttpAuthenticationMethods scheme,
        string userPassword,
        CancellationToken cancellationToken)
    {
        string url = string.Create(CultureInfo.InvariantCulture, $"{context.Url}{sequence:D4}");
        context.StandardError.Write(string.Create(CultureInfo.InvariantCulture, $"Sending new request {sequence} to {url} with credential {userPassword} (auth {(long)scheme})\n"));
        return steps.SetOptionsAndPerformAsync(
            easy,
            [
                (CurlOption.Url, url),
                (CurlOption.Verbose, 1L),
                (CurlOption.Header, 1L),
                (CurlOption.Httpget, 1L),
                (CurlOption.Userpwd, userPassword),
                (CurlOption.Httpauth, (long)scheme),
            ],
            cancellationToken);
    }
}
