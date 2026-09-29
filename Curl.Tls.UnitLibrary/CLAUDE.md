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
TLS 1.3 key schedule (BL-697), the TLS 1.3 client handshake (BL-699, ADR-0146), the
TLS 1.2, 1.1 and 1.0 PRF and record protection (BL-702, ADR-0150), the TLS 1.2,
1.1 and 1.0 client handshake (BL-703, ADR-0154), and TLS 1.3 over a byte stream
(BL-700, ADR-0157).

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
- TLS 1.3 over a byte stream (ADR-0157): `Tls13RecordProtection` is one direction under
  one traffic secret (RFC 8446 section 5.2 nonces, no padding sent, peer padding removed,
  the GCM and ChaCha20-Poly1305 suites; CCM is BL-810's). The internal
  `Tls13RecordLayer` reads whole records off the caller's `Stream` and holds a protection
  per level. `Tls13ClientConnection.ConnectAsync` runs `Tls13ClientHandshake` over it
  (middlebox compatibility `change_cipher_spec` included) and returns a
  `Tls13ConnectResult`: a `Tls13ClientStream` or a `TlsHandshakeFailure` whose `Origin`
  (`TlsHandshakeFailureOrigin`) says who ended it. `Tls13ClientStream` reads and writes
  application data like `SslStream`'s stream, handles NewSessionTicket and KeyUpdate,
  returns 0 at `close_notify` or a bare transport end (`CloseNotifyReceived` tells them
  apart), and throws `TlsAlertException` for any other alert. The AEADs behind
  `ITlsAead` (`AesGcmTlsAead`, `AriaGcmTlsAead`, `ChaCha20Poly1305TlsAead`) serve both
  TLS 1.2 and 1.3.
- `TlsPrf` (`Md5Sha1` for TLS 1.0 and 1.1, `Sha256`, `Sha384`): the PRF of RFC 2246 and
  RFC 5246, the master secret, the extended master secret (RFC 7627), the key block and
  both Finished `verify_data`s. `Tls12KeyBlock.Partition` divides the key block into each
  side's `Tls12WriteKeys` by the lengths `Tls12RecordProtectionParameters` (version,
  `Tls12BulkCipher`, `Tls12MacAlgorithm`, encrypt-then-MAC) implies. ADR-0140 names this
  pair `Tls12RecordLayer`; it is built as the two connection states below.
- `Tls12RecordWriteState` fragments content to 2^14, protects each record and writes
  its header; `Tls12RecordReadState.Unprotect` takes one record's fragment and returns
  the content or `bad_record_mac` / `record_overflow`. Each holds its own sequence number;
  `CreatePlaintext` is the initial state. Record layouts: null with or without a MAC, CBC
  (AES, Camellia, 3DES) MAC-then-encrypt or encrypt-then-MAC (RFC 7366), with TLS 1.0's
  chained IVs or explicit random IVs from `ITlsRandomSource`, and TLS 1.2 AEAD (AES-GCM,
  ARIA-GCM with the sequence number as the explicit nonce, ChaCha20-Poly1305 with the
  XORed nonce). TLS 1.0 CBC writes an empty record before application data unless
  `insertEmptyFragment` is off (`--ssl-allow-beast`). CBC padding is checked with masks
  (`Tls12CbcPadding`); the Lucky Thirteen hash-time residual is BL-795's. AES-CCM and RC4
  records are BL-796's.
- `Tls12ClientHandshake`: the I/O-free TLS 1.2, 1.1 and 1.0 client state machine.
  `Start()` returns the ClientHello; `ReceiveHandshake(bytes)` takes handshake record
  content and `ReceiveChangeCipherSpec(content)` the server's ChangeCipherSpec; each
  returns a `Tls12HandshakeOutput` of `Tls12OutgoingMessage`s (handshake messages and
  the client's ChangeCipherSpec, in order), completion, or a `TlsHandshakeFailure`. It
  exposes `RecordProtection` and `KeyBlock`: the caller switches its write state after
  sending the ChangeCipherSpec and its read state after the server's is accepted. Key
  exchanges: ECDHE (X25519, P-256/384/521), DHE with the server's group (1024 bits at
  least), RSA and anonymous; `Tls12CipherSuite` holds the 73 suites the record layer can
  protect (`Tls12KeyExchange`, `Tls12Authentication`, bulk cipher, MAC, PRF). Covers the
  ServerKeyExchange signature (TLS 1.2 schemes, and TLS 1.0/1.1's MD5+SHA-1 RSA and
  SHA-1 ECDSA), empty `renegotiation_info` (a server without it is refused), extended
  master secret, encrypt-then-MAC, ALPN, SNI, `status_request` with the CertificateStatus
  handed to the verifier with the chain, resumption by session ID and by ticket
  (`Tls12Session`, `Tls12NewSessionTicket`), and an optional client certificate (an RSA
  key cannot yet sign TLS 1.0/1.1's CertificateVerify, so it sends an empty Certificate).
  HelloRequest is ignored. The TLS 1.2 codecs: `Tls12CertificateMessage`,
  `Tls12CertificateRequest`, `Tls12ServerKeyExchange` (`Tls12EcdheParameters`,
  `Tls12DheParameters`), `Tls12ClientKeyExchange`, `Tls12CertificateVerify`.
- `Tls12ClientSettings`: version range, suites (and the renegotiation SCSV), ECDHE
  groups, TLS 1.2 signature algorithms, ALPN, `status_request`, whether to offer
  `session_ticket`, `extended_master_secret` and `encrypt_then_mac`, the session to
  resume, and the client certificate. The ClientHello's extensions follow OpenSSL's order.
- `ITlsRandomSource` supplies the random, session ID, key shares, DHE exponent and RSA
  pre-master secret; `SystemTlsRandomSource` is the production one.
- `IServerCertificateVerifier` gets the chain as a `ServerCertificateChain` (DER
  certificates, SNI name, stapled OCSP response) and answers a `ServerCertificateVerdict`.
- Signatures: `TlsSignatureScheme` (codes, the TLS 1.3 and TLS 1.2 scheme tables - TLS
  1.2 adds `rsa_pkcs1_*` and `ecdsa_sha1` and binds `ecdsa_*` to no curve - TLS 1.0/1.1's
  legacy signatures, and the CertificateVerify content), `TlsCertificatePublicKey` (a
  certificate's `SubjectPublicKeyInfo`, the signature checks, and the RSA pre-master
  secret encryption), `TlsSigningKey` with `RsaTlsSigningKey`, `EcdsaTlsSigningKey` and
  `Ed25519TlsSigningKey`.
- Tests: `Tls13TestServer` and `Tls12TestServer` in `Curl.Tls.UnitTests` are in-memory
  servers built from these codecs; `Tls12TestServer` resumes from a shared
  `Tls12TestSessionCache` and signs TLS 1.0/1.1 RSA with `BigInteger` (test code only).

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
