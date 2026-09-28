namespace Curl.Tls;

/// <summary>
/// One extension as it sits in a handshake message's extension block (RFC 8446 section
/// 4.2): its type and its undecoded data. The per-extension codecs
/// (<see cref="KeyShareExtension" />, <see cref="SupportedVersionsExtension" /> and the
/// rest) turn the data into typed values and back.
/// </summary>
/// <param name="Type">The extension type.</param>
/// <param name="Data">The extension data, without the type and length.</param>
public sealed record TlsExtension(TlsExtensionType Type, byte[] Data);
