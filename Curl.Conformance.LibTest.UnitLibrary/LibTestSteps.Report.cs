using System.Runtime.CompilerServices;
using Libcurl.Easy;
using Libcurl.Misc.ErrorText;

namespace Curl.Conformance.LibTest;

/// <summary>The perform step lib658 and lib659 write their own failure line for.</summary>
public sealed partial class LibTestSteps
{
    /// <summary>
    /// <c>result = curl_easy_perform(curl)</c>, and when it fails, upstream's
    /// <c>%s:%d curl_easy_perform() failed with code %d (%s)</c> on stderr.
    /// </summary>
    /// <param name="easy">The handle.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>Whether the transfer succeeded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="easy"/> is <see langword="null"/>.</exception>
    public async ValueTask<bool> EasyPerformOrReportAsync(
        CurlEasy easy,
        CancellationToken cancellationToken,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        if (await EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        WriteLine(file, line, $"curl_easy_perform() failed with code {(int)Result} ({CurlErrorText.Easy(Result)})");
        return false;
    }
}
