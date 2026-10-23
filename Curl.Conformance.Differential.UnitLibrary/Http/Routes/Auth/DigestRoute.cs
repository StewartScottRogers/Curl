using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Auth;

/// <summary>
/// server.py's <c>/digest</c>: <c>digest ok\n</c> for an MD5 Digest answer (user <c>u</c>, realm <c>r</c>,
/// password <c>p</c>, nonce <c>abc</c>, qop <c>auth</c>), otherwise 401 with that challenge and
/// <c>denied\n</c>; the connection is kept. A field the answer lacks counts as Python's <c>None</c>.
/// </summary>
public sealed class DigestRoute : IDifferentialRoute
{
    /// <inheritdoc/>
    public DifferentialRouteRank Rank => DifferentialRouteRank.Specific;

    /// <inheritdoc/>
    public bool Matches(DifferentialHttpRequest request) => request.Path == "/digest";

    /// <inheritdoc/>
    public async Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken)
    {
        var response = AnswersTheChallenge(context.Request)
            ? new DifferentialHttpResponse(200, [], "digest ok\n"u8.ToArray())
            : new DifferentialHttpResponse(
                401,
                [("WWW-Authenticate", "Digest realm=\"r\", nonce=\"abc\", qop=\"auth\", opaque=\"o\", algorithm=MD5")],
                "denied\n"u8.ToArray());
        await DifferentialHttpResponseWriter.WriteAsync(context.Connection, response, cancellationToken);
        return false;
    }

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new DigestRoute());

    private static bool AnswersTheChallenge(DifferentialHttpRequest request)
    {
        var authorization = request.Headers.GetValueOrDefault("authorization", string.Empty);
        if (!authorization.StartsWith("Digest ", StringComparison.Ordinal))
        {
            return false;
        }

        var fields = DigestAuthorizationFields.Parse(authorization);
        var expected = Md5(
            $"{Md5("u:r:p")}:abc:{fields.GetValueOrDefault("nc", "None")}:{fields.GetValueOrDefault("cnonce", "None")}:auth:"
            + Md5($"{request.Method}:{fields.GetValueOrDefault("uri", "None")}"));
        return fields.GetValueOrDefault("response") == expected;
    }

    private static string Md5(string text) => Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(text)));
}
