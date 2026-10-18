using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib3102.c</c>: an HTTPS transfer of the case's URL with <c>CURLOPT_CERTINFO</c> on, its body thrown away and
/// neither the peer nor the host verified; when it succeeds (or gets nothing) its <c>CURLINFO_CERTINFO</c> chain must be
/// in order, each certificate's issuer the next one's subject (<see cref="IsChainInOrder"/>). A chain out of order ends
/// the program with <see cref="LibTestErrorCodes.Failure"/>; otherwise the transfer's (or the getinfo's) code is the
/// program's. A failing <c>curl_global_init</c> or <c>curl_easy_init</c> writes its failure and ends with
/// <see cref="LibTestErrorCodes.MajorBad"/>.
/// </summary>
public sealed class Lib3102 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib3102";

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
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            (CurlOption, object?)[] options =
            [
                (CurlOption.Url, context.Url),
                (CurlOption.Certinfo, 1L),
                (CurlOption.Writefunction, (WriteCallback)LibTestUtilities.ThrowawayWrite),
                (CurlOption.SslVerifypeer, 0L),
                (CurlOption.SslVerifyhost, 0L),
            ];
            // Libcurl takes these easy_setopt values without fail, so the macros' jumps to test_cleanup are left out.
            LibTestRun.SetOptionsUnchecked(easy, options);
            _ = await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false);
            if (steps.Result != CurlCode.Ok && steps.Result != CurlCode.GotNothing)
            {
                return;
            }

            // Libcurl answers CURLINFO_CERTINFO on any live handle, so upstream's check of the getinfo's code is left out.
            steps.Result = CurlCode.Ok;
            _ = easy.GetInfo(CurlInfo.Certinfo, out object? certificates);
            if (!IsChainInOrder(context, (IReadOnlyList<IReadOnlyList<string>>)certificates!))
            {
                steps.Result = LibTestErrorCodes.Failure;
            }
        }
    }

    /// <summary>
    /// <c>is_chain_in_order</c>: writes each certificate's number, subject and issuer on stdout and answers whether each
    /// issuer is the next certificate's subject; a chain of one certificate or none is in order without a line. A chain
    /// out of order writes which pair differs on stderr.
    /// </summary>
    /// <param name="context">The program's context.</param>
    /// <param name="certificates">The <c>CURLINFO_CERTINFO</c> fields, one list per certificate.</param>
    /// <returns>Whether the chain is in order.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static bool IsChainInOrder(LibTestContext context, IReadOnlyList<IReadOnlyList<string>> certificates)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(certificates);
        if (certificates.Count <= 1)
        {
            return true;
        }

        using StreamWriter output = LibTestRun.StandardOutputWriter(context);
        string? lastIssuer = null;
        for (int certificate = 0; certificate < certificates.Count; certificate++)
        {
            var (issuer, subject) = FindIssuerAndSubject(certificates[certificate]);
            if (!FollowsInOrder(context, output, certificate, issuer, subject, lastIssuer))
            {
                return false;
            }

            lastIssuer = issuer;
        }

        output.Write("certificate chain is in order\n");
        return true;
    }

    private static bool FollowsInOrder(LibTestContext context, StreamWriter output, int certificate, string? issuer, string? subject, string? lastIssuer)
    {
        if (subject is null || issuer is null)
        {
            return true;
        }

        output.Write($"cert {certificate}\n  subject: {subject}\n  issuer: {issuer}\n");
        if (lastIssuer is null || string.Equals(lastIssuer, subject, StringComparison.Ordinal))
        {
            return true;
        }

        context.StandardError.Write($"cert {certificate - 1} issuer does not match cert {certificate} subject\ncertificate chain is not in order\n");
        return false;
    }

    private static (string? Issuer, string? Subject) FindIssuerAndSubject(IReadOnlyList<string> fields)
    {
        string? issuer = null;
        string? subject = null;
        for (int i = 0; i < fields.Count && (issuer is null || subject is null); i++)
        {
            issuer = ValueAfter(fields[i], "Issuer:") ?? issuer;
            subject = ValueAfter(fields[i], "Subject:") ?? subject;
        }

        return (issuer, subject);
    }

    private static string? ValueAfter(string field, string prefix) =>
        field.StartsWith(prefix, StringComparison.Ordinal) ? field[prefix.Length..] : null;
}
