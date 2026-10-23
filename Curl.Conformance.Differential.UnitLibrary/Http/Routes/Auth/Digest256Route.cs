using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Auth;

/// <summary>
/// server.py's <c>/digest256</c>: <c>sha256 ok\n</c> for a SHA-256 Digest answer (user <c>u</c>, realm
/// <c>r</c>, password <c>p</c>, nonce <c>abc</c>, qop <c>auth</c>) naming algorithm <c>SHA-256</c> or
/// <c>SHA256</c>, otherwise 401 with a SHA-256 and an MD5 challenge and <c>denied\n</c>; the connection is
/// kept. A missing <c>nc</c> or <c>cnonce</c> counts as Python's <c>None</c>, a missing <c>uri</c> as empty.
/// </summary>
public sealed class Digest256Route : IDifferentialRoute
{
    /// <inheritdoc/>
    public DifferentialRouteRank Rank => DifferentialRouteRank.Specific;

    /// <inheritdoc/>
    public bool Matches(DifferentialHttpRequest request) => request.Path == "/digest256";

    /// <inheritdoc/>
    public async Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken)
    {
        var response = AnswersTheChallenge(context.Request)
            ? new DifferentialHttpResponse(200, [], "sha256 ok\n"u8.ToArray())
            : new DifferentialHttpResponse(
                401,
                [
                    ("WWW-Authenticate", "Digest realm=\"r\", nonce=\"abc\", qop=\"auth\", algorithm=SHA-256"),
                    ("WWW-Authenticate", "Digest realm=\"r\", nonce=\"abc\", qop=\"auth\", algorithm=MD5"),
                ],
                "denied\n"u8.ToArray());
        await DifferentialHttpResponseWriter.WriteAsync(context.Connection, response, cancellationToken);
        return false;
    }

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new Digest256Route());

    private static bool AnswersTheChallenge(DifferentialHttpRequest request)
    {
        var authorization = request.Headers.GetValueOrDefault("authorization", string.Empty);
        if (!authorization.StartsWith("Digest ", StringComparison.Ordinal))
        {
            return false;
        }

        var fields = DigestAuthorizationFields.Parse(authorization);
        var expected = Sha256(
            $"{Sha256("u:r:p")}:abc:{fields.GetValueOrDefault("nc", "None")}:{fields.GetValueOrDefault("cnonce", "None")}:auth:"
            + Sha256($"{request.Method}:{fields.GetValueOrDefault("uri", string.Empty)}"));
        return fields.GetValueOrDefault("response") == expected
            && fields.GetValueOrDefault("algorithm", string.Empty).ToUpperInvariant() is "SHA-256" or "SHA256";
    }

    private static string Sha256(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
