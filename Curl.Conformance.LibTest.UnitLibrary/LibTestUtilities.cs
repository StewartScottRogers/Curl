using System.Globalization;

namespace Curl.Conformance.LibTest;

/// <summary>
/// curl 8.22.0's <c>tests/libtest/testutil.c</c>: <c>tutil_suburl</c> and <c>tutil_throwaway_cb</c>.
/// <c>tutil_rlim2str</c> is left out: it formats <c>getrlimit</c> values, which no managed program reads.
/// </summary>
public static class LibTestUtilities
{
    /// <summary><c>tutil_suburl(base, i)</c>: <paramref name="baseUrl"/> followed by <paramref name="number"/> in at least four digits (<c>%s%.4d</c>).</summary>
    /// <param name="baseUrl">The URL to extend.</param>
    /// <param name="number">The number to append, such as 1 for <c>0001</c>.</param>
    /// <returns>The request URL.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="baseUrl"/> is <see langword="null"/>.</exception>
    public static string SubUrl(string baseUrl, int number)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        return baseUrl + number.ToString("D4", CultureInfo.InvariantCulture);
    }

    /// <summary><c>tutil_throwaway_cb</c>: a <c>CURLOPT_WRITEFUNCTION</c> that keeps nothing and takes every byte.</summary>
    /// <param name="data">The received bytes.</param>
    /// <returns>The number of bytes taken, all of them.</returns>
    public static long ThrowawayWrite(ReadOnlySpan<byte> data) => data.Length;
}
