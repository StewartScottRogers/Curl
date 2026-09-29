# Curl.Tls.UnitLibrary

A hand-built TLS client for what `SslStream` cannot do, and for QUIC. ADR-0140
(`Documentation/Planning/Decisions/ADR-0140-the-hand-built-tls-client-runs-quic-always-and-tcp-only-where-sslstream-cannot.md`)
decides what it does and when Curl uses it: always inside QUIC, where RFC 9001 hands
handshake bytes to CRYPTO frames and takes secrets per encryption level, and over TCP
only for what `SslStream` cannot offer (`--curves`, `--sigalgs`, `--tls-earlydata`,
`--ssl-sessions`, `--no-sessionid`, `--ech`, TLS-SRP, `--cert-status`, TLS 1.0 and 1.1
where the operating system disables them, `--ssl-allow-beast`). Everything else stays on
`SslStreamTlsProvider` in `Curl.Networking.UnitLibrary`.

Namespace `Curl.Tls`. What is here so far: the handshake message codecs (BL-698), the
TLS 1.3 key schedule (BL-697) and the TLS 1.3 client handshake (BL-699, ADR-0143).

- `HandshakeMessageReader` frames handshake bytes into `HandshakeMessage`s (type and
  body); an unknown type is `unexpected_message`.
- One record per message with `Encode()` (header included) and `Decode(body)`:
  `ClientHello`, `ServerHello` (`IsHelloRetryRequest`), `EncryptedExtensions`,
  `CertificateRequest`, `CertificateMessage`, `CertificateVerify`, `Finished`,
  `NewSessionTicket`.
- One static codec per extension (`KeyShareExtension`, `SupportedVersionsExtension`,
  `PreSharedKeyExtension` and the rest), with a method pair per message shape.
- Decoders return `TlsDecodeResult<T>`: the value or a `TlsAlertDescription`, never an
  exception. They read through the internal `TlsReader`, whose first failure sticks, so
  a decoder reads in a straight line and checks once in `Finish`; a repeated extension
  in one block is `illegal_parameter`, any length past its end is `decode_error`.
- `HkdfLabel` (public, for QUIC): `Encode` builds RFC 8446's `HkdfLabel` info block with
  the `tls13 ` prefix, `Expand` is HKDF-Expand-Label over the BCL's `HKDF`.
- `Tls13KeySchedule` (`Sha256`, `Sha384`): pure functions for RFC 8446 section 7 - the
  early, handshake and master secrets, every Derive-Secret by its RFC name, traffic keys
  and IVs (`Tls13TrafficKeys`), Finished keys and verify data, PSK binders, the
  resumption PSK, and the KeyUpdate secret. Secrets are passed in and returned; the
  handshake holds them.
- `TranscriptHash` accumulates handshake messages (header included) and reads the hash
  without ending it; `ReplaceWithMessageHash` swaps the first ClientHello for the
  `message_hash` message after a HelloRetryRequest.
- `Tls13ClientHandshake`: the I/O-free TLS 1.3 client state machine. `Start()` returns
  the ClientHello; `Receive(level, bytes)` takes server handshake bytes at an encryption
  level (`TlsEncryptionLevel`) and returns a `Tls13HandshakeOutput`: bytes to send per
  level, `Tls13TrafficSecret`s installed per level and direction, completion, or a
  `TlsHandshakeFailure` (the alert to send, and a rejected chain's reason). Covers
  HelloRetryRequest (cookie included), the TLS 1.3 suites (`Tls13CipherSuite`), ALPN,
  SNI, CertificateVerify with RSA-PSS (RSAE and PSS keys), ECDSA P-256/384/521 and
  Ed25519, the server Finished, an optional client certificate
  (`TlsClientCertificate`), and NewSessionTicket after completion.
- `Tls13ClientSettings`: suites, groups, key share groups, signature algorithms, ALPN,
  legacy session ID on or off, the ClientHello extension order and verbatim extra
  extensions (`padding` in the order pads a 256-to-511-byte hello to 512).
- Key shares: `X25519KeyShare`, `EcdhKeyShare` (NIST curves, points checked by
  `NistCurve`), `FfdheKeyShare` (RFC 7919 groups); `TlsNamedGroup` names them.
- `ITlsRandomSource` supplies the random, session ID and key shares;
  `SystemTlsRandomSource` is the production one.
- `IServerCertificateVerifier` gets the chain as a `ServerCertificateChain` (DER
  certificates, SNI name, stapled OCSP response) and answers a `ServerCertificateVerdict`.
- Signatures: `TlsSignatureScheme` (codes and the CertificateVerify content),
  `TlsCertificatePublicKey` (a certificate's `SubjectPublicKeyInfo` and the
  CertificateVerify check), `TlsSigningKey` with `RsaTlsSigningKey`,
  `EcdsaTlsSigningKey` and `Ed25519TlsSigningKey`.

## Rules

- **Base class library plus `Curl.Cryptography.UnitLibrary` only** (ADR-0120). It may
  also reference `Curl.Protocol.Abstractions.UnitLibrary`; nothing else. BL-699 added the
  `Curl.Cryptography.UnitLibrary` reference (X25519, Ed25519, finite-field DH).
  `Curl.Quic.UnitLibrary` and `Curl.Networking.UnitLibrary` reference this library, never
  the other way round.
- **Never a `Socket` or `SslStream`**, and no `HttpClient`. Bytes in, bytes out: record
  layer bytes and handshake messages go in and come out, and the caller owns the
  transport (a TCP connection, or QUIC's CRYPTO frames).
- **Randomness and time are injected.** Client randoms, key shares and session IDs come
  from an injected source, and certificate validity and ticket lifetimes are judged
  against an injected `TimeProvider`, so every handshake is reproducible in a test.
- **RFC 8448 traces are the reference tests.** The example handshakes in RFC 8448 (and
  RFC 9001 appendix A for QUIC) are replayed byte for byte in `Curl.Tls.UnitTests`, with
  the section cited beside each trace. Tests are platform-neutral and never open a
  socket.
- Same quality gates as every library: 100% line and branch coverage, cyclomatic
  complexity of at most 10, CRAP of at most 30.
