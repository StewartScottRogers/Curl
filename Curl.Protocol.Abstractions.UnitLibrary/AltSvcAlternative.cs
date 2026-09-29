namespace Curl.Protocol.Abstractions;

/// <summary>
/// One alternative service an <c>Alt-Svc</c> response header names: the protocol it speaks,
/// and the host and port that serve it (RFC 7838).
/// </summary>
/// <param name="Alpn">
/// The alternative's ALPN protocol ID as curl 8.21.0 names it: <c>h1</c>, <c>h2</c> or <c>h3</c>.
/// </param>
/// <param name="Host">The alternative's host, without IPv6 brackets.</param>
/// <param name="Port">The alternative's port, from 1 to 65535.</param>
public sealed record AltSvcAlternative(string Alpn, string Host, int Port);
