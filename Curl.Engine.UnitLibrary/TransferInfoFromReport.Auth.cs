using Curl.Api.Info;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal static partial class TransferInfoFromReport
{
    /// <summary>
    /// The info with the <c>CURLAUTH_*</c> bits of the schemes used, and of every scheme the server and
    /// the proxy offered across the transfer's authentication retries, as libcurl ORs each challenge
    /// into <c>httpauthavail</c> and <c>proxyauthavail</c> (BL-3258).
    /// </summary>
    private static TransferInfo WithAuth(TransferInfo info, TransferReport report) => info with
    {
        HttpAuthUsed = CurlAuthBitsOf(report.AuthSchemePicked),
        ProxyAuthUsed = CurlAuthBitsOf(report.ProxyAuthSchemePicked),
        HttpAuthAvailable = CurlAuthBitsOf(report.AuthSchemesOffered),
        ProxyAuthAvailable = CurlAuthBitsOf(report.ProxyAuthSchemesOffered),
    };

    /// <summary>The <c>CURLAUTH_*</c> bits of the engine's schemes.</summary>
    private static long CurlAuthBitsOf(HttpAuthSchemes schemes) =>
        (schemes.HasFlag(HttpAuthSchemes.Basic) ? 1 : 0)
        | (schemes.HasFlag(HttpAuthSchemes.Digest) ? 2 : 0)
        | (schemes.HasFlag(HttpAuthSchemes.Negotiate) ? 4 : 0)
        | (schemes.HasFlag(HttpAuthSchemes.Ntlm) ? 8 : 0)
        | (schemes.HasFlag(HttpAuthSchemes.Bearer) ? 64 : 0);
}
