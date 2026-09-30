namespace Curl.Tls;

/// <summary>
/// The user name and password a TLS-SRP handshake authenticates with (RFC 5054,
/// <c>--tlsuser</c> and <c>--tlspassword</c>). Both enter SRP as their UTF-8 bytes, as
/// OpenSSL takes curl's strings, with no SASLprep.
/// </summary>
/// <param name="UserName">The SRP user name I, sent in the <c>srp</c> extension.</param>
/// <param name="Password">The SRP password P.</param>
public sealed record TlsSrpCredentials(string UserName, string Password);
