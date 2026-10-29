namespace Curl.Extension.OAuth2.Wire;

/// <summary>The client a request is made for (RFC 6749 §2.2, §2.3.1).</summary>
/// <param name="ClientId">The client identifier.</param>
/// <param name="ClientSecret">The client password, or <see langword="null"/> for a public client.</param>
/// <param name="Authentication">How a client with a password sends it.</param>
public sealed record OAuth2Client(string ClientId, string? ClientSecret, OAuth2ClientAuthentication Authentication);
