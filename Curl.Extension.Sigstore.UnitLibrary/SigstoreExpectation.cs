namespace Curl.Extension.Sigstore;

/// <summary>What the user asked a download to be verified against.</summary>
/// <param name="BundlePath">The Sigstore bundle's path.</param>
/// <param name="Identity">The identity the signing certificate must name, or <see langword="null" /> when not given.</param>
/// <param name="Issuer">The OIDC issuer the signing certificate must name, or <see langword="null" /> for any.</param>
/// <param name="RootPath">The PEM file of trusted Fulcio CA certificates, or <see langword="null" /> when not given.</param>
internal sealed record SigstoreExpectation(string BundlePath, string? Identity, string? Issuer, string? RootPath);
