namespace Curl.Tls;

/// <summary>One key share (RFC 8446 section 4.2.8): a named group and its public key exchange value.</summary>
/// <param name="Group">The named group code point.</param>
/// <param name="KeyExchange">The public value, encoded as the group defines.</param>
public sealed record KeyShareEntry(ushort Group, byte[] KeyExchange);
