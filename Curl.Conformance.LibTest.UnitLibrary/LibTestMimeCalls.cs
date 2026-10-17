using Curl.Api.Codes;

namespace Curl.Conformance.LibTest;

/// <summary>
/// The <c>if(!result) result = curl_mime_...(...)</c> chains lib643 and lib652 build a part with: each call
/// runs only while the ones before it succeeded.
/// </summary>
public static class LibTestMimeCalls
{
    /// <summary>Runs <paramref name="calls"/> in order until one fails.</summary>
    /// <param name="calls">The part's calls, each answering its <see cref="CurlCode"/>.</param>
    /// <param name="failed">The index of the call that failed, or -1 when none did.</param>
    /// <returns>The failing call's code, or <see cref="CurlCode.Ok"/> when every call succeeded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="calls"/> is <see langword="null"/>.</exception>
    public static CurlCode FirstFailure(IReadOnlyList<Func<CurlCode>> calls, out int failed)
    {
        ArgumentNullException.ThrowIfNull(calls);
        for (failed = 0; failed < calls.Count; failed++)
        {
            CurlCode code = calls[failed]();
            if (code != CurlCode.Ok)
            {
                return code;
            }
        }

        failed = -1;
        return CurlCode.Ok;
    }
}
