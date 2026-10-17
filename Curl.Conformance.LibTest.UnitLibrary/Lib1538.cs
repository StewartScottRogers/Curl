using Curl.Api.Codes;
using Libcurl.Misc.ErrorText;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1538.c</c>: the four <c>*_strerror</c> calls given <c>INT_MAX</c> and <c>-INT_MAX</c>, which must not fail,
/// then one stdout line per code: <c>eN: </c> for <c>CURLE_OK</c> to <c>CURL_LAST</c>, <c>mN: </c> for
/// <c>CURLM_CALL_MULTI_PERFORM</c> to <c>CURLM_LAST</c>, <c>sN: </c> for <c>CURLSHE_OK</c> to <c>CURLSHE_LAST</c> and
/// <c>uN: </c> for <c>CURLUE_OK</c> to <c>CURLUE_LAST</c>, each followed by its text. It calls no
/// <c>curl_global_init</c> and always succeeds.
/// </summary>
public sealed class Lib1538 : ILibTest
{
    /// <summary>curl 8.22.0's <c>CURL_LAST</c>.</summary>
    public const int EasyLast = 102;

    /// <summary>curl 8.22.0's <c>CURLM_LAST</c>.</summary>
    public const int MultiLast = 13;

    /// <summary>curl 8.22.0's <c>CURLSHE_LAST</c>.</summary>
    public const int ShareLast = 6;

    /// <summary>curl 8.22.0's <c>CURLUE_LAST</c>.</summary>
    public const int UrlLast = 33;

    /// <inheritdoc/>
    public string ProgramName => "lib1538";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        foreach (int outOfRange in (int[])[int.MaxValue, -int.MaxValue])
        {
            _ = CurlErrorText.Easy((CurlCode)outOfRange);
            _ = CurlErrorText.Multi((CurlMultiCode)outOfRange);
            _ = CurlErrorText.Share((CurlShareCode)outOfRange);
            _ = CurlErrorText.Url((CurlUrlCode)outOfRange);
        }

        using StreamWriter output = LibTestRun.StandardOutputWriter(context);
        for (int code = 0; code <= EasyLast; code++)
        {
            output.WriteLine($"e{code}: {CurlErrorText.Easy((CurlCode)code)}");
        }

        for (int code = -1; code <= MultiLast; code++)
        {
            output.WriteLine($"m{code}: {CurlErrorText.Multi((CurlMultiCode)code)}");
        }

        for (int code = 0; code <= ShareLast; code++)
        {
            output.WriteLine($"s{code}: {CurlErrorText.Share((CurlShareCode)code)}");
        }

        for (int code = 0; code <= UrlLast; code++)
        {
            output.WriteLine($"u{code}: {CurlErrorText.Url((CurlUrlCode)code)}");
        }

        return Task.FromResult(LibTestResult.Success);
    }
}
