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
1.1 and 1.0 client handshake (BL-703, ADR-0154), TLS 1.3 over a byte stream
(BL-700, ADR-0157), TLS 1.2, 1.1 and 1.0 over a byte stream (BL-815, ADR-0249), and
the stapled OCSP response check for `--cert-status` (BL-705, ADR-0173), TLS 1.3
certificate decompression (BL-786, ADR-0199), TLS 1.3 and TLS 1.2 offered in one
ClientHello (BL-821, ADR-0205), post-handshake client authentication (BL-880),
TLS-SRP (BL-704, ADR-0229), and Encrypted Client Hello (BL-706, ADR-0233).

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
  `message_hash` message after a HelloRetryRequest; `Clone` copies it for a post-handshake
  exchange that must not change it.
- `Tls13ClientHandshake`: the I/O-free TLS 1.3 client state machine. `Start()` returns
  the ClientHello; `Receive(level, bytes)` takes server handshake bytes at an encryption
  level (`TlsEncryptionLevel`) and returns a `Tls13HandshakeOutput`: bytes to send per
  level, `Tls13TrafficSecret`s installed per level and direction, completion, or a
  `TlsHandshakeFailure` (the alert to send, and a rejected chain's reason). Covers
  HelloRetryRequest (cookie included), the TLS 1.3 suites (`Tls13CipherSuite`), ALPN,
  SNI, CertificateVerify with RSA-PSS (RSAE and PSS keys), ECDSA P-256/384/521 and
  brainpoolP256r1/384r1/512r1 (RFC 8734), Ed25519, Ed448 and ML-DSA-44/65/87, the server Finished, an optional client certificate
  (`TlsClientCertificate`), and NewSessionTicket after completion. With `post_handshake_auth`
  in `Tls13ClientSettings.ExtensionOrder` the hello offers it, and a CertificateRequest
  after completion (its context non-empty, else `illegal_parameter`) is answered at the
  Application level with Certificate (echoing the context), CertificateVerify when a
  certificate fits, and Finished keyed from the client application traffic secret in force,
  over a clone of the handshake transcript plus the request (RFC 8446 section 4.6.2); not
  offered, it is `unexpected_message`.
- Certificate compression (RFC 8879, ADR-0199): `CompressedCertificate` is the message
  codec, and `Decompress(offered)` returns the Certificate body through `ZLibStream`,
  `BrotliDecoder` or `Curl.Zstandard`'s `ZstandardDecoder` when the algorithm was offered
  and the data decompresses to exactly `uncompressed_length`. With
  `Tls13ClientSettings.CertificateCompressionAlgorithms` (codes in
  `CertificateCompressionAlgorithm`) the ClientHello offers `compress_certificate` and the
  handshake takes a CompressedCertificate in place of the Certificate; anything that does
  not decompress is `bad_certificate`.
- `ClientHelloProfile` (BL-787): ADR-0140's three measured hellos as data -
  `Schannel`, `OpenSsl` (OpenSSL 3.5.5) and `LibreSsl` (curl.se's LibreSSL 4.2.1 build):
  record version, suites, extension order and each extension's list. `Build(host, random,
  sessionId, keyShares)` returns the build's ClientHello byte for byte; `EncodeRecord`
  wraps it in the record header. Options change the lists with `with`, never the order.
  The typed codecs those hellos needed: `EcPointFormatsExtension`,
  `SessionTicketExtension`, `ExtendedMasterSecretExtension`, `EncryptThenMacExtension`,
  `PostHandshakeAuthExtension`, `CompressCertificateExtension`; also
  `CertificateAuthoritiesExtension` and `SrpExtension` from ADR-0140's supported list.
- `Tls13ClientSettings`: suites, groups, key share groups, signature algorithms, ALPN,
  legacy session ID on or off, the ClientHello extension order and verbatim extra
  extensions (`padding` in the order pads a 256-to-511-byte hello to 512).
- Key shares: `X25519KeyShare`, `X448KeyShare`, `EcdhKeyShare` (NIST curves, points
  checked by `NistCurve`), `BrainpoolKeyShare` (TLS 1.2's brainpool curves over
  `Curl.Cryptography`'s `BrainpoolEcdh`, BL-803, and their RFC 8734 `tls13` groups,
  BL-1049), `FfdheKeyShare` (RFC 7919 groups),
  `X25519MlKem768KeyShare` (BL-879: the ML-KEM-768 encapsulation key then the X25519
  key out, the 1088-byte ciphertext then the server's X25519 key in, the ML-KEM secret
  then the X25519 secret as the shared secret), `MlKemKeyShare` (MLKEM512/768/1024: the
  encapsulation key out, the ciphertext in) and `EcdhMlKemKeyShare` (SecP256r1MLKEM768 and
  SecP384r1MLKEM1024, curve first: the point then the encapsulation key out, the server's
  point then the ciphertext in, the ECDH secret then the ML-KEM secret; BL-1049);
  `TlsNamedGroup` names them. A server share of the wrong length is `illegal_parameter`.
  The server's encapsulations live in the tests (`X25519MlKem768ServerShare`,
  `MlKemServerShare`), since the client never needs them; `KeyShareKnownAnswers` holds
  OpenSSL 3.5.5's known answers for the new shares.
- TLS 1.3 over a byte stream (ADR-0157): `Tls13RecordProtection` is one direction under
  one traffic secret (RFC 8446 section 5.2 nonces, no padding sent, peer padding removed,
  every TLS 1.3 suite: GCM, ChaCha20-Poly1305, CCM, and CCM8 with its 8-byte tag,
  BL-811). The internal
  `Tls13RecordLayer` reads whole records off the caller's `Stream` and holds a protection
  per level. `Tls13ClientConnection.ConnectAsync` runs `Tls13ClientHandshake` over it
  (middlebox compatibility `change_cipher_spec` included) and returns a
  `Tls13ConnectResult`: a `Tls13ClientStream` or a `TlsHandshakeFailure` whose `Origin`
  (`TlsHandshakeFailureOrigin`) says who ended it. `Tls13ClientStream` reads and writes
  application data like `SslStream`'s stream, handles NewSessionTicket, KeyUpdate and a
  post-handshake CertificateRequest (answered under the record layer's write lock, so a
  KeyUpdate cannot slip between keying the Finished and sending it),
  returns 0 at `close_notify` or a bare transport end (`CloseNotifyReceived` tells them
  apart), and throws `TlsAlertException` for any other alert. The AEADs behind
  `ITlsAead` (`AesGcmTlsAead`, `AriaGcmTlsAead`, `ChaCha20Poly1305TlsAead`) serve both
  TLS 1.2 and 1.3; `AesCcmTlsAead` (over `Curl.Cryptography`'s `AeadAesCcm`) serves both too.
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
  ARIA-GCM and AES-CCM/CCM8 with the sequence number as the explicit nonce,
  ChaCha20-Poly1305 with the XORed nonce), and RC4 stream records
  (`Tls12StreamRecordCipher`, the keystream running on across records). TLS 1.0 CBC
  writes an empty record before application data unless `insertEmptyFragment` is off (`--ssl-allow-beast`). CBC padding is checked with masks
  (`Tls12CbcPadding`) and the MAC with `Tls12RecordMac.VerifyInFixedBlocks`, which hashes
  the same number of blocks whatever the padding (`FixedBlockHmac`, BL-795).
- `Tls12ClientHandshake`: the I/O-free TLS 1.2, 1.1 and 1.0 client state machine.
  `Start()` returns the ClientHello; `ReceiveHandshake(bytes)` takes handshake record
  content and `ReceiveChangeCipherSpec(content)` the server's ChangeCipherSpec; each
  returns a `Tls12HandshakeOutput` of `Tls12OutgoingMessage`s (handshake messages and
  the client's ChangeCipherSpec, in order), completion, or a `TlsHandshakeFailure`. It
  exposes `RecordProtection` and `KeyBlock`: the caller switches its write state after
  sending the ChangeCipherSpec and its read state after the server's is accepted. Key
  exchanges: ECDHE (X25519, P-256/384/521, and x448 and brainpoolP256r1/384r1/512r1 when
  offered, ADR-0219), DHE with the server's group (1024 bits at
  least, authenticated by RSA or DSA), RSA, anonymous and SRP; `Tls12CipherSuite` holds the 113
  suites the record layer can protect (`Tls12KeyExchange`, `Tls12Authentication`, bulk
  cipher, MAC, PRF). Covers the ServerKeyExchange signature (TLS 1.2 schemes, and TLS
  1.0/1.1's MD5+SHA-1 RSA and SHA-1 ECDSA and DSA; DSA is checked with
  `Curl.Cryptography`'s `DsaSignature`, ADR-0211), empty `renegotiation_info` (a server without it is refused), extended
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
  resume, the client certificate, TLS 1.0 CBC's empty fragment
  (`InsertEmptyFragment`, off for `--ssl-allow-beast`), `SrpCredentials`, and `PadHello`
  (a 256-to-511-byte hello padded to 512, `padding` last, ADR-0365). The
  ClientHello's extensions follow OpenSSL's order.
- TLS-SRP (RFC 5054, ADR-0229): `SrpGroup` holds Appendix A's seven groups (`Find` by N
  and g), `SrpClient` SRP-6a's pure functions (k, x, v, A, u and the premaster secret S,
  SHA-1 over `BigInteger`). With `Tls12ClientSettings.SrpCredentials`
  (`TlsSrpCredentials`) the hello carries `srp` after `server_name` and the nine SRP
  suites (`Tls12KeyExchange.Srp`; plain SRP is `Tls12Authentication.Anonymous`, the others
  RSA or DSS); without it they are left out. `Tls12SrpParameters` is the ServerKeyExchange's
  N, g, s and B: a group outside Appendix A is `insufficient_security`, a B of 0 or not
  below N `illegal_parameter`, and a wrong password fails the server's Finished with
  `decrypt_error`.
- TLS 1.2 and below over a byte stream (ADR-0249): the internal `Tls12RecordLayer` reads
  whole records off the caller's `Stream`, removes their protection with the read state in
  force and writes under the write state in force; the ClientHello record carries TLS 1.0,
  later records the negotiated version, and a record read with another is
  `protocol_version`. `Tls12ClientConnection.ConnectAsync` runs `Tls12ClientHandshake`
  over it, switching each state at its ChangeCipherSpec, and returns a
  `Tls12ConnectResult`: a `Tls12ClientStream` or a `TlsHandshakeFailure` with its
  `Origin`. `Tls12ClientStream` behaves as `Tls13ClientStream` does (0 at `close_notify`
  or a bare transport end, `CloseNotifyReceived`, `TlsAlertException`, `ShutdownAsync`)
  and ignores HelloRequest.
- One ClientHello for both (ADR-0205): `TlsClientConnection.ConnectAsync` takes a
  `TlsClientSettings` (a `Tls13ClientSettings` and a `Tls12ClientSettings` with a TLS 1.2
  ceiling and no session to resume) and sends the TLS 1.3 hello with the TLS 1.2 half added
  through the internal `Tls13ClientSettings.LowerVersions` (versions in
  `supported_versions`, suites, signature schemes, and the TLS 1.2 extensions the TLS 1.3
  order does not build). It reads the server's first handshake message through the internal
  `ServerHelloReplayStream`, which then replays what it read: a ServerHello without
  `supported_versions` continues in `Tls12ClientHandshake.StartFrom(sent)` (either
  downgrade sentinel is `illegal_parameter`), anything else in the TLS 1.3 client. It returns
  a `TlsConnectResult` with a `Tls13ClientStream` or a `Tls12ClientStream`, or the failure.
- Encrypted Client Hello (RFC 9849, ADR-0233): `EchConfigList.Decode` reads an
  `ECHConfigList` (malformed is `decode_error`, other versions skipped) into `EchConfig`s
  with their `EchCipherSuite`s; `SupportedConfig` is the first one HPKE can seal to.
  With `Tls13ClientSettings.EncryptedClientHelloConfigs` holding one (and
  `encrypted_client_hello` in the order), the internal `EchClientHello` builds the inner
  hello (real SNI, TLS 1.3 alone, section 6.1.3 padding) and seals it with
  `Curl.Cryptography`'s `Hpke` into the outer hello (the public name, the same key shares);
  the handshake checks the confirmation in the HelloRetryRequest and ServerHello, runs on
  the inner hello when accepted (`EncryptedClientHelloAccepted`), and on a rejection
  verifies the chain for the public name, keeps `EncryptedClientHelloRetryConfigs` and
  fails with `EchRequired` after the server's Finished. `SendEncryptedClientHelloGrease`
  sends GREASE when no config is supported. An ECH offer resumes (BL-960): the ticket and
  its binder (over the inner transcript) go in the inner hello, a GREASE `pre_shared_key`
  of the same lengths in the outer one, `early_data` in both or neither, and early data
  under the inner hello's early secret; a `pre_shared_key` in a ServerHello that rejected
  ECH is `illegal_parameter`. `EncryptedClientHelloExtension` is the extension codec.
- `ITlsRandomSource` supplies the random, session ID, key shares, DHE exponent, SRP private value, RSA
  pre-master secret and ECH's inner random, HPKE ephemeral key and GREASE bytes;
  `SystemTlsRandomSource` is the production one.
- `IServerCertificateVerifier` gets the chain as a `ServerCertificateChain` (DER
  certificates, SNI name, stapled OCSP response) and answers a `ServerCertificateVerdict`.
- OCSP stapling (ADR-0173): `OcspStapleVerifier.Verify(response, chain, now)` checks a
  stapled response (RFC 6960) in curl's OpenSSL order and returns an
  `OcspStapleOutcome` (`OcspStapleStatus` and the CRL reason or `responseStatus`). It
  reads with the internal `OcspBasicResponse`, `OcspSingleResponse` and
  `OcspCertificateFields`, and maps signature and `CertID` hash OIDs with
  `OcspSignatureAlgorithm`. With `RequestOcspStatus` (and the settings' `TimeProvider`)
  both handshakes run it once the verifier accepts the chain, expose `CertificateStatus`,
  and fail anything but good with `bad_certificate_status_response` and
  `TlsHandshakeFailure.CertificateStatusRejection`.
- Signatures: `TlsSignatureScheme` (codes, the TLS 1.3 and TLS 1.2 scheme tables - the
  brainpool `tls13` schemes and `mldsa44/65/87` are TLS 1.3 only; TLS 1.2 adds
  `rsa_pkcs1_*` (SHA-224 included), `ecdsa_sha1`, `ecdsa_sha224` and the five `dsa_*`
  (verify only) and binds `ecdsa_*` to no curve; `ed448` is in both, so every scheme of
  `ClientHelloProfile.OpenSsl` is checkable (BL-940) - TLS 1.0/1.1's
  legacy signatures, and the CertificateVerify content), `TlsCertificatePublicKey` (a
  certificate's `SubjectPublicKeyInfo`, the signature checks - a brainpool ECDSA key with
  `Curl.Cryptography`'s `BrainpoolEcdsa`, ADR-0219, Ed448 and ML-DSA with its `Ed448`
  and `MlDsa`, SHA-224 hashed with its `DsaSignature.HashData`, and `rsa_pkcs1_sha224`'s
  DigestInfo block checked with the public operation as TLS 1.0's MD5+SHA-1 block is -
  and the RSA pre-master secret encryption), `TlsSigningKey` with `RsaTlsSigningKey`, `EcdsaTlsSigningKey`,
  `Ed25519TlsSigningKey`, `Ed448TlsSigningKey` and `MlDsaTlsSigningKey` (the last two over
  `Curl.Cryptography`'s `Ed448` and `MlDsa`, empty context, BL-1064).
- Tests: `Tls13TestServer` and `Tls12TestServer` in `Curl.Tls.UnitTests` are in-memory
  servers built from these codecs; `Tls12TestServer` resumes from a shared
  `Tls12TestSessionCache` and signs TLS 1.0/1.1 RSA with `BigInteger` (test code only).
  `Tls13TestServer.ConfirmEch` makes it an ECH backend, and `EchTestFrontEnd` (with
  `EchTestConfig`) is the client-facing server that opens the outer hello for it.
  `Tls13RecordTestServer` and `Tls12RecordTestServer` put them on the server end of an
  `InMemoryPipe`.

## Rules

- **Base class library plus `Curl.Cryptography.UnitLibrary` and `Curl.Zstandard.UnitLibrary`
  only** (ADR-0120, amended by ADR-0185). It may also reference
  `Curl.Protocol.Abstractions.UnitLibrary`; nothing else. BL-699 added the
  `Curl.Cryptography.UnitLibrary` reference (X25519, Ed25519, finite-field DH), BL-786 the
  `Curl.Zstandard.UnitLibrary` one (zstd certificate decompression).
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
