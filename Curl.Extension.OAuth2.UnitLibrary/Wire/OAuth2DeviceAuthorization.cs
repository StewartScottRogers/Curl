namespace Curl.Extension.OAuth2.Wire;

/// <summary>A device authorization response (RFC 8628 §3.2).</summary>
/// <param name="DeviceCode">The device verification code.</param>
/// <param name="UserCode">The end-user verification code.</param>
/// <param name="VerificationUri">The URI the user visits.</param>
/// <param name="VerificationUriComplete">The URI with the user code in it, or <see langword="null"/> when the server sent none.</param>
/// <param name="ExpiresIn">How many seconds the codes last.</param>
/// <param name="Interval">How many seconds to wait between polls: the server's, or 5 when it sent none.</param>
public sealed record OAuth2DeviceAuthorization(
    string DeviceCode,
    string UserCode,
    string VerificationUri,
    string? VerificationUriComplete,
    long ExpiresIn,
    long Interval);
