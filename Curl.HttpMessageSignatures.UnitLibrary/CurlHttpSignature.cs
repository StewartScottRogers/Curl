using System.Globalization;

namespace Curl.HttpMessageSignatures;

/// <summary>
/// Signs a request as curl 8.22.0's <c>Curl_output_httpsig</c> does for the <c>--httpsig-*</c>
/// options: label <c>sig1</c>, parameters <c>created</c>, <c>keyid</c> and <c>alg</c>.
/// </summary>
public static class CurlHttpSignature
{
    /// <summary>The signature label curl uses (<c>HTTPSIG_DEFAULT_LABEL</c>).</summary>
    public const string Label = "sig1";

    /// <summary>The length in bytes of an Ed25519 key.</summary>
    public const int Ed25519KeyLength = 32;

    /// <summary>
    /// Returns the <c>Signature-Input</c> and <c>Signature</c> values for <paramref name="request" />,
    /// or <see langword="null" /> when a <c>-H</c> line already sets <c>Signature</c> or
    /// <c>Signature-Input</c>, in which case curl adds neither.
    /// </summary>
    /// <param name="algorithm">The <c>--httpsig-algo</c> algorithm.</param>
    /// <param name="hexKey">The <c>--httpsig-key</c> text (the file's text for <c>@file</c>), or <see langword="null" />.</param>
    /// <param name="keyId">The <c>--httpsig-keyid</c> text, or <see langword="null" />.</param>
    /// <param name="componentsText">The <c>--httpsig-headers</c> text, or <see langword="null" /> for the default set.</param>
    /// <param name="request">The request to sign.</param>
    /// <param name="created">The <c>created</c> time in Unix seconds.</param>
    /// <returns>The header values, or <see langword="null" />.</returns>
    /// <exception cref="HttpSignatureException">curl refuses to sign, with its message; exit code 43.</exception>
    public static HttpSignatureHeaders? CreateHeaders(HttpSignatureAlgorithm algorithm, string? hexKey, string? keyId, string? componentsText, HttpSignatureRequest request, long created)
    {
        ArgumentNullException.ThrowIfNull(request);
        var key = RequireOption(hexKey, "httpsig: CURLOPT_HTTPSIG_KEY is required");
        var id = RequireOption(keyId, "httpsig: CURLOPT_HTTPSIG_KEYID is required");
        if (request.HasHeader("Signature") || request.HasHeader("Signature-Input"))
        {
            return null;
        }

        var keyBytes = DecodeKey(algorithm, key);
        try
        {
            var components = HttpSignatureComponents.Parse(componentsText, request.Query is not null);
            var parameters = HttpSignatureBase.FormatParameters(components, created, id, HttpSignatureAlgorithmNames.ToName(algorithm));
            var values = components.Select(component => KeyValuePair.Create(component, ComponentValue(component, request))).ToList();
            var signatureBase = HttpSignatureBase.Build(values, parameters);
            return HttpSignatureHeaders.Create(Label, parameters, signatureBase, HttpMessageSigner.Sign(algorithm, keyBytes, signatureBase));
        }
        finally
        {
            Array.Clear(keyBytes);
        }
    }

    private static string RequireOption(string? value, string message) =>
        string.IsNullOrEmpty(value) ? throw new HttpSignatureException(message) : value;

    private static byte[] DecodeKey(HttpSignatureAlgorithm algorithm, string hexKey)
    {
        var key = HttpSignatureKey.DecodeHex(hexKey);
        if (algorithm == HttpSignatureAlgorithm.Ed25519 && key.Length != Ed25519KeyLength)
        {
            throw new HttpSignatureException(string.Create(CultureInfo.InvariantCulture, $"httpsig: ed25519 requires a 32-byte key (got {key.Length})"));
        }

        return key;
    }

    private static string ComponentValue(string component, HttpSignatureRequest request) => component switch
    {
        HttpSignatureComponents.Method => request.Method,
        HttpSignatureComponents.Authority => request.Authority,
        HttpSignatureComponents.Path => request.SignedPath,
        HttpSignatureComponents.Query => request.SignedQuery,
        _ => request.HeaderValue(component),
    };
}
