namespace Curl.Core.Hsts;

/// <summary>The two fields of one line of curl's HSTS file, as written.</summary>
/// <param name="Host">The host field, a leading dot (subdomains included) and trailing dot kept.</param>
/// <param name="ExpiryText">The text between the quotes: a date, or <c>unlimited</c>.</param>
public sealed record HstsFileLine(string Host, string ExpiryText);
