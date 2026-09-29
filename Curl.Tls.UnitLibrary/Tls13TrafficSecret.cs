namespace Curl.Tls;

/// <summary>A traffic secret the handshake installs: from here on, bytes at <paramref name="Level" /> in <paramref name="Direction" /> are protected with it.</summary>
/// <param name="Level">The encryption level the secret protects.</param>
/// <param name="Direction">Whether it protects what the client reads or writes.</param>
/// <param name="Secret">The traffic secret; the record layer or QUIC derives its key and IV.</param>
public sealed record Tls13TrafficSecret(TlsEncryptionLevel Level, TlsTrafficDirection Direction, byte[] Secret);
