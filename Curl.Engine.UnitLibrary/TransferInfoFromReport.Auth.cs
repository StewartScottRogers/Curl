using Curl.Api.Info;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal static partial class TransferInfoFromReport
{
    /// <summary>The <c>CURLAUTH_*</c> bit of each scheme name a challenge starts with.</summary>
    private static readonly Dictionary<string, long> CurlAuthBitsByChallengeScheme = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Basic"] = 1,
        ["Digest"] = 2,
        ["Negotiate"] = 4,
        ["NTLM"] = 8,
        ["Bearer"] = 64,
    };

    /// <summary>The info with the <c>CURLAUTH_*</c> bits of the schemes used and offered by the server and the proxy.</summary>
    private static TransferInfo WithAuth(TransferInfo info, TransferReport report) => info with
    {
        HttpAuthUsed = CurlAuthBitsOf(report.AuthSchemePicked),
        ProxyAuthUsed = CurlAuthBitsOf(report.ProxyAuthSchemePicked),
        HttpAuthAvailable = report.ResponseCode == 401 ? ChallengedCurlAuthBits(report, "WWW-Authenticate") : 0,
        ProxyAuthAvailable = report.ResponseCode == 407 ? ChallengedCurlAuthBits(report, "Proxy-Authenticate") : 0,
    };

    /// <summary>The <c>CURLAUTH_*</c> bits of the engine's schemes.</summary>
    private static long CurlAuthBitsOf(HttpAuthSchemes schemes) =>
        (schemes.HasFlag(HttpAuthSchemes.Basic) ? 1 : 0)
        | (schemes.HasFlag(HttpAuthSchemes.Digest) ? 2 : 0)
        | (schemes.HasFlag(HttpAuthSchemes.Negotiate) ? 4 : 0)
        | (schemes.HasFlag(HttpAuthSchemes.Ntlm) ? 8 : 0)
        | (schemes.HasFlag(HttpAuthSchemes.Bearer) ? 64 : 0);

    /// <summary>The <c>CURLAUTH_*</c> bits of the schemes the last response's <paramref name="headerName" /> lines offer.</summary>
    private static long ChallengedCurlAuthBits(TransferReport report, string headerName) =>
        report.ResponseHeaders
            .Where(header => string.Equals(header.Key, headerName, StringComparison.OrdinalIgnoreCase))
            .Select(header => CurlAuthBitsByChallengeScheme.GetValueOrDefault(header.Value.Trim().Split(' ')[0]))
            .Aggregate(0L, (bits, bit) => bits | bit);
}
