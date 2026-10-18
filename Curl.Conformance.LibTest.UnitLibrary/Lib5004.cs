using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib5004.c</c>: <see cref="LibHttpSignatureRequest"/> as RFC 9421's Ed25519 test vector: a JSON POST with fixed
/// <c>Date</c>, <c>Content-Type</c> and <c>Content-Length</c> headers, all of them covered with the method, path and
/// authority (<see cref="CoveredComponents"/>).
/// </summary>
public sealed class Lib5004() : LibHttpSignatureRequest(5004)
{
    /// <summary>The <c>CURLOPT_HTTPSIG_HEADERS</c> list of covered components.</summary>
    public const string CoveredComponents = "date: method path authority content-type: content-length:";

    /// <summary>The body the program posts.</summary>
    public const string Body = "{\"hello\": \"world\"}";

    /// <inheritdoc/>
    protected override IEnumerable<(CurlOption Option, object? Value)> ProgramOptions()
    {
        CurlSlist headers = CurlSlist.Append(null, "Date: Tue, 20 Apr 2021 02:07:55 GMT");
        headers = CurlSlist.Append(headers, "Content-Type: application/json");
        headers = CurlSlist.Append(headers, "Content-Length: 18");
        return
        [
            (CurlOption.HttpsigHeaders, CoveredComponents),
            (CurlOption.Postfields, Body),
            (CurlOption.Httpheader, headers),
        ];
    }
}
