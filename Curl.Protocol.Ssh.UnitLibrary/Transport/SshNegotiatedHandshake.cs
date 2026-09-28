using Curl.Protocol.Ssh.Negotiation;

namespace Curl.Protocol.Ssh.Transport;

/// <summary>
/// What the key exchange needs from the handshake before it: the two identification
/// strings and <c>KEXINIT</c> payloads that go into the exchange hash (RFC 4253 section
/// 8: <c>V_C</c>, <c>V_S</c>, <c>I_C</c>, <c>I_S</c>), and the agreed algorithms.
/// </summary>
/// <param name="ClientIdentification">The client's identification string, without CR LF.</param>
/// <param name="ServerIdentification">The server's identification string, without CR LF.</param>
/// <param name="ClientKexInitPayload">The client's <c>KEXINIT</c> payload as sent.</param>
/// <param name="ServerKexInitPayload">The server's <c>KEXINIT</c> payload as received.</param>
/// <param name="Algorithms">The agreed algorithms.</param>
internal sealed record SshNegotiatedHandshake(
    string ClientIdentification,
    string ServerIdentification,
    byte[] ClientKexInitPayload,
    byte[] ServerKexInitPayload,
    SshNegotiatedAlgorithms Algorithms);
