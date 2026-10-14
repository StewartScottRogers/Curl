namespace Curl.Protocol.Abstractions;

/// <summary>
/// What the <c>--httpsig-*</c> options ask for once curl 8.22.0's setup checks pass: every
/// request to the origin is signed with an RFC 9421 HTTP message signature in place of every
/// other scheme (BL-2182, ADR-0485).
/// </summary>
/// <param name="Algorithm">The <c>--httpsig-algo</c> algorithm in lower case, <c>ed25519</c> (the default) or <c>hmac-sha256</c>.</param>
/// <param name="Key">The <c>--httpsig-key</c> text as hexadecimal digits; for <c>@file</c>, the file's text.</param>
/// <param name="KeyId">The <c>--httpsig-keyid</c> text, verbatim.</param>
/// <param name="Components">The <c>--httpsig-headers</c> text, verbatim, or <see langword="null" /> for curl's default components.</param>
public sealed record HttpSignatureSettings(string Algorithm, string Key, string KeyId, string? Components);
