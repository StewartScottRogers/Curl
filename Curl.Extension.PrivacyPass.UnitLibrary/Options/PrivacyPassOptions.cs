using System.Security.Cryptography;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.PrivacyPass.Options;

/// <summary>
/// The Privacy Pass extension's option, its description and its transfer hook activator (ADR-1095):
/// <c>--privacy-pass &lt;issuer request URL&gt;</c> answers a <c>PrivateToken</c> challenge with a token from that issuer.
/// </summary>
public static class PrivacyPassOptions
{
    /// <summary>Gets <c>--privacy-pass &lt;issuer request URL&gt;</c>.</summary>
    public static ExtensionOption Issuer { get; } = new(
        "privacy-pass",
        ExtensionOptionArgument.Value,
        "Answer a 401 PrivateToken challenge with a Privacy Pass token (Blind RSA) from the issuer at this request URL");

    /// <summary>Gets the extension's description.</summary>
    public static ExtensionDescription Description { get; } = new(
        "privacy-pass",
        "Answers an HTTP 401 PrivateToken challenge with a publicly verifiable Privacy Pass token (Blind RSA, token type 2) issued by the --privacy-pass issuer, and sends the request again with it.",
        [],
        [Issuer],
        new Uri("https://www.rfc-editor.org/rfc/rfc9578"));

    /// <summary>Gives the hook when <c>--privacy-pass</c> was given, otherwise <see langword="null"/>.</summary>
    /// <param name="values">The option group's extension option values.</param>
    /// <returns>The hook, or <see langword="null"/>.</returns>
    public static ITransferHook? ActivatedBy(ExtensionOptionValues values) => ActivatedBy(values, RandomNumberGenerator.GetBytes);

    /// <summary>Gives the hook when <c>--privacy-pass</c> was given, otherwise <see langword="null"/>.</summary>
    /// <param name="values">The option group's extension option values.</param>
    /// <param name="randomBytes">Gives cryptographically random octets.</param>
    /// <returns>The hook, or <see langword="null"/>; a hook whose value is not an absolute URL fails its transfer with exit 2.</returns>
    internal static ITransferHook? ActivatedBy(ExtensionOptionValues values, Func<int, byte[]> randomBytes)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.ValueOf(Issuer) is { } value
            ? new PrivacyPassTransferHook(Uri.TryCreate(value, UriKind.Absolute, out var url) ? url : null, randomBytes)
            : null;
    }
}
