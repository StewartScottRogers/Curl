namespace Curl.HttpMessageSignatures;

/// <summary>
/// The two header field values curl 8.22.0 adds to a signed request, and the texts its verbose
/// output shows on the way.
/// </summary>
/// <param name="SignatureInput">The <c>Signature-Input</c> value, such as <c>sig1=("@method");created=1;keyid="k";alg="ed25519"</c>.</param>
/// <param name="Signature">The <c>Signature</c> value, such as <c>sig1=:base64:</c>.</param>
/// <param name="SignatureParameters">The signature parameters: <see cref="SignatureInput" /> without its label.</param>
/// <param name="SignatureBase">The signature base that was signed.</param>
public sealed record HttpSignatureHeaders(
    string SignatureInput,
    string Signature,
    string SignatureParameters,
    string SignatureBase)
{
    /// <summary>
    /// Labels <paramref name="signatureParameters" /> and <paramref name="signature" /> as one
    /// signature, the signature written as an RFC 8941 byte sequence (<c>:base64:</c>).
    /// </summary>
    /// <param name="label">The signature label, such as <c>sig1</c>.</param>
    /// <param name="signatureParameters">The text <see cref="HttpSignatureBase.FormatParameters" /> wrote.</param>
    /// <param name="signatureBase">The signature base that was signed.</param>
    /// <param name="signature">The raw signature.</param>
    /// <returns>The header values.</returns>
    public static HttpSignatureHeaders Create(string label, string signatureParameters, string signatureBase, ReadOnlySpan<byte> signature) =>
        new(
            label + "=" + signatureParameters,
            label + "=:" + Convert.ToBase64String(signature) + ":",
            signatureParameters,
            signatureBase);
}
