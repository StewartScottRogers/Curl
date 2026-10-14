using System.Globalization;
using System.Text;

namespace Curl.HttpMessageSignatures;

/// <summary>
/// The RFC 9421 signature parameters (section 2.3) and signature base (section 2.5), written
/// as curl 8.22.0's <c>build_sig_params</c> and <c>build_sig_base</c> write them.
/// </summary>
public static class HttpSignatureBase
{
    /// <summary>
    /// Writes the signature parameters: the quoted component identifiers in parentheses, then
    /// <c>;created=</c>, <c>;keyid=</c> when <paramref name="keyId" /> is not empty, and
    /// <c>;alg=</c> when <paramref name="algorithmName" /> is given, in that order.
    /// </summary>
    /// <param name="componentIdentifiers">The identifiers, in signing order.</param>
    /// <param name="created">The <c>created</c> time in Unix seconds.</param>
    /// <param name="keyId">The <c>keyid</c>, or <see langword="null" /> to leave it out.</param>
    /// <param name="algorithmName">The <c>alg</c>, or <see langword="null" /> to leave it out.</param>
    /// <returns>Such as <c>("@method" "@path");created=1618884473;keyid="k";alg="ed25519"</c>.</returns>
    /// <exception cref="HttpSignatureException">An identifier or the key id holds a control character.</exception>
    public static string FormatParameters(IReadOnlyList<string> componentIdentifiers, long created, string? keyId, string? algorithmName)
    {
        var parameters = new StringBuilder("(");
        parameters.AppendJoin(' ', componentIdentifiers.Select(Quote));
        parameters.Append(CultureInfo.InvariantCulture, $");created={created}");
        if (!string.IsNullOrEmpty(keyId))
        {
            parameters.Append(";keyid=").Append(Quote(keyId));
        }

        if (algorithmName is not null)
        {
            parameters.Append(";alg=").Append(Quote(algorithmName));
        }

        return parameters.ToString();
    }

    /// <summary>
    /// Writes the signature base: one <c>"identifier": value</c> line per component, each ending
    /// in a line feed, then <c>"@signature-params": </c> and the parameters with no line feed.
    /// </summary>
    /// <param name="componentValues">Each component identifier with its value, in signing order.</param>
    /// <param name="signatureParameters">The text <see cref="FormatParameters" /> wrote.</param>
    /// <returns>The bytes to sign, as text.</returns>
    public static string Build(IReadOnlyList<KeyValuePair<string, string>> componentValues, string signatureParameters)
    {
        var signatureBase = new StringBuilder();
        foreach (var (identifier, value) in componentValues)
        {
            signatureBase.Append('"').Append(identifier).Append("\": ").Append(value).Append('\n');
        }

        return signatureBase.Append("\"@signature-params\": ").Append(signatureParameters).ToString();
    }

    // An RFC 8941 string, as upstream's sf_append_quoted writes it: a backslash before each
    // quote or backslash, and a control character refused.
    private static string Quote(string text)
    {
        if (text.Any(IsControl))
        {
            throw new HttpSignatureException(HttpSignatureException.BadArgumentMessage);
        }

        return "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    // ISCNTRL in curl: 0x00 to 0x1f, and 0x7f.
    private static bool IsControl(char character) => character < (char)0x20 || character == (char)0x7f;
}
