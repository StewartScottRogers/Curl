# ADR-0146 — The TLS 1.3 client handshake takes its key shares and extension order from its caller

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-699.

## Context

ADR-0140 names `Tls13ClientHandshake` as an I/O-free state machine that takes handshake
bytes per encryption level and returns bytes to send, secrets installed and completion or
a `TlsHandshakeFailure`, with randomness from an injected `ITlsRandomSource` and the
chain handed to `IServerCertificateVerifier`. Building it left six questions ADR-0140 does
not answer:

1. How tests replay RFC 8448's ephemeral keys. Section 5's second ClientHello carries a
   P-256 key share, and importing a P-256 private key without its public point is not
   supported the same way on Windows, Linux and macOS.
2. How one handshake reproduces both RFC 8448 hellos byte for byte and, later, the three
   measured ClientHello profiles (BL-698 did not add `ClientHelloProfile`), whose
   extension orders all differ.
3. Where `padding` goes and how long it is: RFC 8448 section 5's second hello is padded
   from 333 to 512 bytes.
4. How the client refuses an ECDH point that is off the curve. Measured 2026-09-28 on
   Windows 11 with .NET 10: `ECDiffieHellman.Create(ECParameters)` with an off-curve
   point throws `PlatformNotSupportedException` ("The specified curve 'nistP256' or its
   parameters are not valid for this platform"), not `CryptographicException`.
5. How a failure is reported, and which alerts the extension checks send.
6. What the handshake does after it completes.

## Decision

1. **Key shares come from the random source.** `ITlsRandomSource` has two members,
   `Fill(Span<byte>)` for the random and session ID and `CreateKeyShare(ushort group)`
   for each ephemeral share. `SystemTlsRandomSource` draws X25519 and finite-field
   private keys from `RandomNumberGenerator` and generates NIST keys with
   `ECDiffieHellman.Create(curve)`. Tests hand over `X25519KeyShare`, `EcdhKeyShare`
   (built from the trace's private key and public point together) and `FfdheKeyShare`
   directly. The groups are X25519, secp256r1/384r1/521r1 and ffdhe2048 to ffdhe8192;
   every group in `SupportedGroups` must be one of them, so a HelloRetryRequest can never
   pick a group the client cannot share.
2. **Extensions go out in the caller's order.** `Tls13ClientSettings.ExtensionOrder`
   lists extension types. The handshake builds `server_name`, `supported_groups`,
   `key_share`, `supported_versions`, `signature_algorithms`, ALPN, `cookie` and
   `padding` itself; every other type is sent from `FixedExtensions` as given, and a
   fixed extension whose type is not listed goes after the listed ones. A listed type
   with nothing to send (no server name, no ALPN, no cookie yet) is left out. So RFC
   8448's hellos are settings, not code, and BL-708's profiles will be too.
3. **Padding follows the NSS and BoringSSL rule** that RFC 8448's trace shows: when
   `padding` is listed and the unpadded ClientHello (header included) is 256 to 511
   bytes, a `padding` extension brings it to 512, or is one byte long when fewer than
   five bytes are missing. It sits at its listed place.
4. **NIST points are checked by hand before the BCL sees them.** `NistCurve` holds each
   curve's prime and `b` and checks `x, y < p` and `y² = x³ - 3x + b`. This runs on
   public values only and refuses a bad point with `illegal_parameter` the same way on
   every platform, with no exception filter that differs by operating system.
5. **Failures are returned, never thrown.** Each step returns a `Tls13HandshakeOutput`;
   a failure carries the alert to send and, for a rejected chain, the verifier's reason
   untouched (`TlsHandshakeFailure.CertificateRejection`). Only a caller mistake (a
   `null`, settings that cannot drive a handshake, `Start` twice, `Receive` before
   `Start`) throws. The alerts follow RFC 8446: an extension the client did not offer is
   `unsupported_extension`; an extension RFC 8446 section 4.2 does not allow in
   EncryptedExtensions is `illegal_parameter`; a ServerHello without
   `supported_versions` is `protocol_version`, or `illegal_parameter` when its random
   ends with a downgrade sentinel; an empty server Certificate is `decode_error`; a
   bad CertificateVerify or Finished is `decrypt_error`.
6. **After completion the handshake takes only NewSessionTicket**, at the Application
   level, and keeps it in `ReceivedTickets` for BL-701. KeyUpdate belongs to BL-700's
   record layer; until then any other post-handshake message is `unexpected_message`.

CertificateVerify is checked against the leaf's `SubjectPublicKeyInfo`, read with
`System.Formats.Asn1` (`TlsCertificatePublicKey`), so RSA-PSS keys (`id-RSASSA-PSS`)
and Ed25519 keys read the same on every platform: `rsa_pss_rsae_*` needs an
`rsaEncryption` key, `rsa_pss_pss_*` an `id-RSASSA-PSS` key, `ecdsa_*` a key on the
scheme's curve, and `ed25519` an Ed25519 key.

## Consequences

- RFC 8448 sections 3 and 5 replay byte for byte from settings alone, and QUIC's
  `quic_transport_parameters` is one `FixedExtensions` entry.
- The handshake has no clock: nothing in the full handshake is judged against time.
  BL-701 brings `TimeProvider` in with ticket ages.
- `NistCurve` carries three curves' constants. They are checked by the tests that agree
  shared secrets on each curve and refuse off-curve and out-of-range points.
- BL-700 must handle KeyUpdate before the handshake passes post-handshake messages on.

## Alternatives considered

- **Draw NIST private keys from `Fill` and import them without the public point.** One
  seam fewer, but the import is not portable (question 1). Rejected.
- **A fixed extension order per profile, in code.** Would need code for every profile and
  trace. Rejected for the order as data.
- **Catch the BCL's exception for an off-curve point.** The exception type differs by
  platform, so the catch either misses one or catches too much. Rejected for the explicit
  check.
