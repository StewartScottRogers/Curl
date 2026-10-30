namespace Curl.Extension.OciRegistry.Auth;

/// <summary>
/// A registry's <c>WWW-Authenticate: Bearer</c> challenge (Docker/Distribution token authentication specification, "How to
/// authenticate", step 3): where to ask for a token and for what.
/// </summary>
/// <param name="Realm">The token endpoint's URL.</param>
/// <param name="Service">The <c>service</c> to ask the token for, or <see langword="null"/> when the challenge names none.</param>
/// <param name="Scope">The <c>scope</c> to ask the token for, such as <c>repository:library/alpine:pull</c>, or <see langword="null"/>.</param>
public sealed record OciBearerChallenge(string Realm, string? Service, string? Scope);
