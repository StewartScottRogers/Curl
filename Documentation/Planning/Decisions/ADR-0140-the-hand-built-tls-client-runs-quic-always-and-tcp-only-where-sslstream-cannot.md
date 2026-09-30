# ADR-0140 — The hand-built TLS client in `Curl.Tls.UnitLibrary` runs QUIC always and TCP only where `SslStream` cannot

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-695.

## Context

Curl is a complete reimplementation of curl (Stewart, 2026-09-28): nothing is left out
because the base class library has no primitive for it. Today every TLS connection goes
through `SslStreamTlsProvider` in `Curl.Networking.UnitLibrary`, which delegates to
`SslStream`, and so to Schannel on Windows, OpenSSL on Linux and Apple's stack on macOS.
`SslStream` cannot:

- run TLS inside QUIC (RFC 9001 section 4 hands handshake bytes to QUIC CRYPTO frames and
  takes secrets per encryption level; `SslStream` only speaks records over a `Stream`);
- choose the key-exchange groups or signature algorithms per connection (`--curves`,
  `--sigalgs`);
- send 0-RTT early data, export or import a session (`--tls-earlydata`,
  `--ssl-sessions`), or stop session reuse between transfers (`--no-sessionid`: Windows
  caches sessions process-wide);
- offer Encrypted Client Hello (`--ech`), TLS-SRP (`--tlsuser`, `--tlspassword`,
  `--tlsauthtype`) or OCSP stapling with the response handed back (`--cert-status`);
- offer TLS 1.0 or 1.1 where the operating system disables them, or turn off the TLS 1.0
  CBC 1/n-1 record split (`--ssl-allow-beast`).

curl builds with OpenSSL, LibreSSL, wolfSSL or GnuTLS control all of these, and curl.se's
official Windows build (curl 8.22.0 with LibreSSL 4.3.2 and ngtcp2,
https://curl.se/windows/, checked 2026-09-28) does HTTP/3. ADR-0009 keeps TLS behaviour
and text matching the platform's usual curl build (Schannel on Windows, OpenSSL on Linux
and macOS); ADR-0118 puts the primitives the BCL lacks in `Curl.Cryptography.UnitLibrary`;
ADR-0120 already gives `Curl.Tls.UnitLibrary` its place in the reference graph (it may
reference `Curl.Cryptography.UnitLibrary` and `Curl.Protocol.Abstractions.UnitLibrary`,
and is referenced by `Curl.Quic.UnitLibrary` and `Curl.Networking.UnitLibrary`).

### The ClientHellos curl's builds send

Measured 2026-09-28 with `Record-CurlExchange.ps1` in its plain server mode: the server
reads whatever curl sends (a ClientHello never ends in CRLF CRLF, so the read ends after
its one-second wait), records it in `request.bin`, and answers with a TLS
`handshake_failure` alert (`\x15\x03\x03\x00\x02\x02\x28`). The URL was
`https://localhost:<port>/` so the hello carries SNI; every run ended in exit 35.

| Build | Command | stderr |
| --- | --- | --- |
| curl.se's official Windows build, curl 8.18.0, LibreSSL 4.2.1, ngtcp2 1.21.0 (the WinGet `cURL.cURL` package; 8.22.0 with LibreSSL 4.3.2 is the current release) | `-Curl <WinGet curl.exe> -CurlArgs '-sS','https://localhost:18443/'` | `curl: (35) TLS connect error: error:14004410:SSL routines:CONNECT_CR_SRVR_HELLO:sslv3 alert handshake failure` |
| mingw curl 8.21.0, Schannel (the Windows reference, ADR-0018) | `-CurlArgs '-sS','https://localhost:18444/'` | `curl: (35) schannel: next InitializeSecurityContext failed: SEC_E_ILLEGAL_MESSAGE (0x80090326) - This error usually occurs when a fatal SSL/TLS alert is received (e.g. handshake failed). More detail may be available in the Windows System event log.` |
| Ubuntu curl 8.18.0, OpenSSL 3.5.5, under WSL | `-ListenAddress 172.26.96.1 -Curl wsl.exe -CurlArgs '-d','Ubuntu','--','curl','-sS','--resolve','localhost:18445:172.26.96.1','https://localhost:18445/'` | `curl: (35) TLS connect error: error:0A000410:SSL routines::ssl/tls alert handshake failure` |

Decoded (code points in hexadecimal, in the order sent). All three send a 32-byte legacy
session ID, legacy version `0x0303`, the null compression method only, and
`supported_versions` `0304 0303`: none offers TLS 1.0 or 1.1 by default.

**LibreSSL 4.2.1** (record version `0x0303`, 309 bytes):

- Cipher suites (46): `1302 1303 1301 c030 c02c c028 c024 c014 c00a 009f 006b 0039 cca9
  cca8 ccaa 00c4 0088 009d 003d 0035 00c0 0084 c02f c02b c027 c023 c013 c009 009e 0067
  0033 00be 0045 009c 003c 002f 00ba 0041 c011 c007 0005 c012 c008 0016 000a 00ff` -
  including Camellia (`00c4 0088 00c0 0084 00be 0045 00ba 0041`), RC4 (`c011 c007
  0005`), 3DES (`c012 c008 0016 000a`) and the renegotiation SCSV `00ff`.
- Extensions: `signature_algorithms` (13), `server_name` (0), `ec_point_formats` (11:
  uncompressed), `key_share` (51: `001d`), `supported_versions` (43),
  `supported_groups` (10), `application_layer_protocol_negotiation` (16: `h2`,
  `http/1.1`).
- `supported_groups`: `001d 0017 0018 0019` (x25519, secp256r1, secp384r1, secp521r1).
- `signature_algorithms`: `0806 0601 0603 0805 0501 0503 0804 0401 0403 0201 0203`.

**Schannel, Windows 11** (record version `0x0301`, 456 bytes):

- Cipher suites (20): `1302 1301 c02c c02b c030 c02f c024 c023 c028 c027 c00a c009 c014
  c013 009d 009c 003d 003c 0035 002f`.
- Extensions: `server_name` (0), `status_request` (5, OCSP, empty lists),
  `supported_versions` (43), `signature_algorithms` (13), `session_ticket` (35, empty),
  `supported_groups` (10), `ec_point_formats` (11), ALPN (16: `http/1.1`), `key_share`
  (51: `001d` 32 bytes, `0017` 65 bytes, `0018` 97 bytes), `post_handshake_auth` (49),
  `extended_master_secret` (23), `renegotiation_info` (65281), `psk_key_exchange_modes`
  (45: `psk_dhe_ke`).
- `supported_groups`: `001d 0017 0018`.
- `signature_algorithms`: `0804 0805 0806 0401 0501 0201 0403 0503 0203 0202 0601 0603`.

**OpenSSL 3.5.5** (record version `0x0301`, 1569 bytes):

- Cipher suites (30): `1302 1303 1301 c02c c030 009f cca9 cca8 ccaa c02b c02f 009e c024
  c028 006b c023 c027 0067 c00a c014 0039 c009 c013 0033 009d 009c 003d 003c 0035 002f`.
- Extensions: `renegotiation_info` (65281), `server_name` (0), `ec_point_formats` (11:
  uncompressed, ansiX962_compressed_prime, ansiX962_compressed_char2), `supported_groups`
  (10), ALPN (16: `h2`, `http/1.1`), `encrypt_then_mac` (22), `extended_master_secret`
  (23), `post_handshake_auth` (49), `signature_algorithms` (13), `supported_versions`
  (43), `psk_key_exchange_modes` (45: `psk_dhe_ke`), `key_share` (51: `11ec`
  X25519MLKEM768 1216 bytes, `001d` 32 bytes), `compress_certificate` (27: zlib,
  zstd).
- `supported_groups`: `11ec 001d 0017 001e 0018 0019 0100 0101` (X25519MLKEM768,
  x25519, secp256r1, x448, secp384r1, secp521r1, ffdhe2048, ffdhe3072).
- `signature_algorithms`: `0905 0906 0904 0403 0503 0603 0807 0808 081a 081b 081c 0809
  080a 080b 0804 0805 0806 0401 0501 0601 0303 0301 0302 0402 0502 0602` (ML-DSA-65,
  -87, -44; ECDSA P-256/384/521; Ed25519; Ed448; the brainpool TLS 1.3 schemes; RSA-PSS
  with PSS keys; RSA-PSS with RSAE keys; RSA PKCS#1 SHA-256/384/512; the SHA-224
  schemes; DSA).

The raw bytes (the random, session ID and key shares differ on every run):

```text
LibreSSL 4.2.1
16030301300100012c0303d6b8f746101d9144e53df3ad5bd3e73b5c51a39dbc499ec8ad049ed692b969b6201bdd3f3893799988f6e86bd9d164df715c927e4eda868e83c83d81536e545884005c130213031301c030c02cc028c024c014c00a009f006b0039cca9cca8ccaa00c40088009d003d003500c00084c02fc02bc027c023c013c009009e0067003300be0045009c003c002f00ba0041c011c0070005c012c0080016000a00ff01000087000d00180016080606010603080505010503080404010403020102030000000e000c0000096c6f63616c686f7374000b00020100003300260024001d0020399ce519cb9d0ffab9d3ded478a27cf46e3d2cdf9ff34de0f80d685c2855e80c002b00050403040303000a000a0008001d0017001800190010000e000c02683208687474702f312e31

Schannel (Windows 11, curl 8.21.0)
16030101c3010001bf0303b87008b2784fa83d52bb21596b41f88b0bc30b23dce3b50d780434224c4be5fe2030f1b9a25b28972dbe1624088642528acddfbb1e0a9801818be41bb3cee1df4c002813021301c02cc02bc030c02fc024c023c028c027c00ac009c014c013009d009c003d003c0035002f0100014e0000000e000c0000096c6f63616c686f7374000500050100000000002b00050403040303000d001a001808040805080604010501020104030503020302020601060300230000000a00080006001d00170018000b000201000010000b000908687474702f312e31003300d000ce001d00202b0f62427470b052fd7516bf732d72fc0d5438b0e6ced503e6530a375d7d512c0017004104964a6a7f68e3c191788a434128cdc8a2b4ae0ef52149d9d7eeaca045b2415c9cd5249b7f41607f83192bc563b51eeb2e3f4f75bdf9553b33ecd3efd6da47484800180061041d0faf506f70d0093645aa03a28006f27a2b61c3eb57a92581cbf4eeeaff5618dae57bb0b80b087f32008ae58a5e909a86b792c326000dcba4dc279e7c00a87c5433ce8cda35c10fdf77d120022e75fcf7a1e1eed6d437c2faefeda50d0524be0031000000170000ff01000100002d00020101

OpenSSL 3.5.5 (Ubuntu curl 8.18.0); the 1216-byte X25519MLKEM768 key share is elided
160301061c010006180303144ae9da9aee4e9aa064b7b6bb8d449e9ca68bd43b5ac3b8ae044536fdb6999d20fe793b4558d3ea23de61ca238d28b1431874c2c5c19432603514be5d62364c27003c130213031301c02cc030009fcca9cca8ccaac02bc02f009ec024c028006bc023c0270067c00ac0140039c009c0130033009d009c003d003c0035002f01000593ff010001000000000e000c0000096c6f63616c686f7374000b000403000102000a0012001011ec001d0017001e00180019010001010010000e000c02683208687474702f312e31001600000017000000310000000d0036003409050906090404030503060308070808081a081b081c0809080a080b080408050806040105010601030303010302040205020602002b00050403040303002d00020101003304ea04e811ec04c0 … 001d002072e3a456c8f1d674c88d3b3b6decb2175a3217e875f685544aaa0ee4cc2de209001b00050400010003
```

OpenSSL 3.5.5's full cipher list (`openssl ciphers -v 'ALL:COMPLEMENTOFALL'`, 158
suites, measured the same day) adds, beyond the default hello: AES-CCM and CCM8, ARIA-GCM,
Camellia-CBC, DHE-DSS, anonymous DH and ECDH (`ADH-*`, `AECDH-*`), SRP, the PSK families
and the NULL suites. Its TLS 1.3 names also include `TLS_AES_128_CCM_SHA256` and
`TLS_AES_128_CCM_8_SHA256`.

## Decision

### The routing rule

`SslStream` stays the default wherever it can do what the command line asks, so every
transfer that works today keeps producing the same bytes. A transfer's TLS runs on the
hand-built client, and only then, when one of these holds:

1. **QUIC.** Every QUIC connection (`--http3`, `--http3-only`, an Alt-Svc upgrade to
   `h3`) uses the hand-built TLS 1.3 handshake. There is no `SslStream` route for QUIC.
2. **An option `SslStream` cannot honour is in force** for that connection, for the
   origin or, with the proxy forms of the options, for an HTTPS proxy (ADR-0095 routes a
   proxy handshake the same way with its own options). The rows known today:

   | Row | Condition | Why `SslStream` cannot | Built by |
   | --- | --- | --- | --- |
   | Groups | `--curves` given | no per-connection group control | BL-699, BL-703, BL-709 |
   | Signature algorithms | `--sigalgs` given | no per-connection control | BL-699, BL-703, BL-709 |
   | Early data | `--tls-earlydata` given | no 0-RTT | BL-701, BL-710 |
   | Session file | `--ssl-sessions` given | no session export or import | BL-701, BL-703, BL-710 |
   | ECH | `--ech` given with any mode other than `false` | no ECH | BL-706, BL-711 |
   | SRP | `--tlsuser` or `--tlspassword` given (`--tlsauthtype SRP` is the only type) | no SRP | BL-704, BL-712 |
   | OCSP stapling | `--cert-status` given | the stapled response is not exposed | BL-705, BL-610 |
   | No session reuse | `--no-sessionid` given | the OS caches sessions for the process | BL-701, BL-703, BL-713 |
   | BEAST split off | `--ssl-allow-beast` given and the version range includes TLS 1.0 | the split cannot be turned off | BL-702, BL-713 |
   | Legacy versions | the version range's maximum (`--tls-max`) is TLS 1.0 or 1.1 | the OS stack refuses them (ADR-0138 measured exit 35) | BL-702, BL-703, BL-714 |

   BL-617 decides each option's row in full (what each official build does, the text on
   each platform) and may add rows, for example `--ciphers` naming a suite the platform's
   `SslStream` cannot offer. Every row is an entry in one pure routing function
   (BL-708) with a data-row test, so each option task adds a row and nothing else. A
   range that reaches TLS 1.2 or above stays on `SslStream`, which negotiates what it can
   as today; BL-714 measures a legacy-only server and may add a row for it.
3. Nothing else. A transfer with none of these conditions never runs the hand-built
   client, so a bug in it cannot break a transfer that works today.

### Default ClientHello: the platform curl's, as measured

The hand-built client builds its ClientHello from a **ClientHello profile**: an ordered
list of extensions and default lists of versions, suites, groups, signature algorithms
and key shares. Three profiles are data in `Curl.Tls.UnitLibrary`, each copied from the
capture above:

- `ClientHelloProfile.Schannel`: the Schannel hello. Used over TCP on Windows, because
  there the hand-built client stands in for the platform's curl (ADR-0009, ADR-0018).
- `ClientHelloProfile.OpenSsl`: the OpenSSL 3.5.5 hello. Used over TCP on Linux and macOS.
- `ClientHelloProfile.LibreSsl`: the LibreSSL hello of curl.se's official build. Used for
  QUIC on Windows, where the Schannel build has no HTTP/3 and curl.se's build (LibreSSL
  with ngtcp2) is the curl that does it. QUIC on Linux and macOS uses the OpenSSL profile.

A QUIC ClientHello keeps only the profile's TLS 1.3 parts (TLS 1.3 suites, groups,
signature algorithms, key shares), drops what RFC 9001 section 8 forbids
(`early_data` other than as RFC 9001 allows, the legacy session ID, `ec_point_formats`,
compatibility mode), and adds `quic_transport_parameters` (57). BL-724 pins the exact
QUIC ClientHello by capturing a real Initial from curl.se's build and removing its
Initial protection, which RFC 9001 section 5.2 derives from public values.

Options change the profile's lists, never its extension order: `--curves` replaces the
groups (and the key shares become the first of them the profile would share),
`--sigalgs` the signature algorithms, `--ciphers`/`--tls13-ciphers` the suites,
`--tlsv1.x`/`--tls-max` the versions, `--cert-status` adds `status_request` where the
profile lacks it, `--no-alpn` removes ALPN, SRP adds `srp` (12), ECH wraps the hello as
the ECH specification says. An extension the profile lacks is appended after the
profile's last extension and before `pre_shared_key`, which RFC 8446 section 4.2.11
requires to be last.

### What the client supports

Nothing any official curl build offers is refused. The supported set is the union of
OpenSSL 3.5.5's full list, LibreSSL's and Schannel's hellos, and the extensions curl's
options need:

| Area | Supported |
| --- | --- |
| Versions | TLS 1.3 (RFC 8446), TLS 1.2 (RFC 5246), TLS 1.1 (RFC 4346), TLS 1.0 (RFC 2246). SSL 2 and SSL 3 are not built: curl 8 lists `--sslv2` and `--sslv3` as deprecated and ignores them (Curl parses them as no-function flags, `CommandLineOptionTable`), so no official build offers them. |
| TLS 1.3 suites | `TLS_AES_128_GCM_SHA256`, `TLS_AES_256_GCM_SHA384`, `TLS_CHACHA20_POLY1305_SHA256`, `TLS_AES_128_CCM_SHA256`, `TLS_AES_128_CCM_8_SHA256` |
| TLS 1.2 and below suites | Every suite in OpenSSL 3.5.5's `ALL:COMPLEMENTOFALL` list whose key exchange curl can drive (ECDHE, DHE, RSA, anonymous DH and ECDH, SRP) with every bulk cipher it names (AES-CBC, AES-GCM, AES-CCM, AES-CCM8, ChaCha20-Poly1305, Camellia-CBC, ARIA-GCM, NULL), plus LibreSSL's RC4 and 3DES suites. The PSK families are not offered because curl has no option that supplies a PSK identity, so no curl build ever offers them; they are the only suites of that list left out, and a future curl option that supplies one adds them. `TLS_EMPTY_RENEGOTIATION_INFO_SCSV` where the profile sends it. |
| Groups | x25519, x448, secp256r1, secp384r1, secp521r1, brainpoolP256r1/384r1/512r1 (TLS 1.2 codes 26 to 28 and the TLS 1.3 `…tls13` codes), ffdhe2048 to ffdhe8192, MLKEM512, MLKEM768, MLKEM1024, X25519MLKEM768, SecP256r1MLKEM768, SecP384r1MLKEM1024; finite-field DHE with server-chosen parameters in TLS 1.2 |
| Signature algorithms | RSA PKCS#1 v1.5 (SHA-1, SHA-224, SHA-256, SHA-384, SHA-512), RSA-PSS with RSAE and PSS keys, ECDSA P-256/384/521 (and SHA-1, SHA-224), Ed25519, Ed448, the brainpool TLS 1.3 schemes, ML-DSA-44/65/87, DSA; TLS 1.0/1.1's MD5+SHA-1 RSA signature |
| Extensions | `server_name`, `status_request`, `supported_groups`, `ec_point_formats`, `signature_algorithms`, `signature_algorithms_cert` (decoded when sent by a server), `application_layer_protocol_negotiation`, `encrypt_then_mac` (RFC 7366), `extended_master_secret` (RFC 7627), `compress_certificate` (RFC 8879), `session_ticket` (RFC 5077), `pre_shared_key`, `early_data`, `supported_versions`, `cookie`, `psk_key_exchange_modes`, `certificate_authorities` (decoded), `post_handshake_auth`, `key_share`, `renegotiation_info` (RFC 5746, initial handshake only: curl never renegotiates), `srp` (RFC 5054), `quic_transport_parameters` (RFC 9001), `encrypted_client_hello` and `ech_outer_extensions` (RFC 9849, TLS Encrypted Client Hello, published 2026-03) |
| Session resumption | TLS 1.3 tickets with `psk_dhe_ke` and binders; TLS 1.2 session IDs and tickets; 0-RTT early data on TLS 1.3 |
| Other | HelloRetryRequest, KeyUpdate (both directions), `close_notify`, post-handshake client authentication, client certificates (RSA, ECDSA, Ed25519, Ed448, DSA keys), the TLS 1.3 downgrade sentinels (RFC 8446 section 4.1.3), the TLS 1.0 CBC 1/n-1 split on by default, record padding off (as all three builds) |

Primitives come from the BCL where ADR-0118 says they are taken from it, and otherwise
from `Curl.Cryptography.UnitLibrary`. This decision **amends ADR-0118's list** with the
primitives the table needs that ADR-0118 did not name: Camellia (RFC 3713, BL-783) and
ARIA with GCM over it (RFC 5794, RFC 6209, BL-784). Certificate decompression needs zlib
(BCL `ZLibStream`), Brotli (BCL `BrotliDecoder`) and Zstandard, which the BCL lacks:
BL-785 decides where the hand-built Zstandard decoder lives, and BL-786 decompresses
certificates with all three.

### Class structure

Namespace `Curl.Tls`, in `Curl.Tls.UnitLibrary`, referencing the BCL,
`Curl.Cryptography.UnitLibrary` and nothing else it needs (ADR-0120 also allows
`Curl.Protocol.Abstractions.UnitLibrary`, which this design does not use). No `Socket`,
no `SslStream`, no file: bytes and a caller's `Stream` in, bytes and a `Stream` out.
Randomness comes through an injected `ITlsRandomSource` (production fills from
`RandomNumberGenerator`; tests replay RFC 8448's randoms and ephemeral keys) and time
through `TimeProvider`.

| Layer | Types | Does |
| --- | --- | --- |
| Codecs | `ClientHelloEncoder`, `HandshakeMessageReader`, one codec per extension, `TlsAlert` | Bytes to typed records and back; a malformed message is a `decode_error` alert value, never an exception. |
| Profiles and settings | `ClientHelloProfile`, `TlsClientSettings` | The three measured profiles; one immutable settings record per connection (SNI host, version range, suites, groups, signature algorithms, ALPN, client certificate and key, `status_request`, session to resume, early data, ECH configuration or GREASE, SRP credentials, BEAST split, session reuse off). |
| TLS 1.3 key schedule | `Tls13KeySchedule`, `HkdfLabel`, `TranscriptHash` | RFC 8446 section 7, SHA-256 and SHA-384; `HkdfLabel.Expand` is public so QUIC derives `quic key`, `quic iv`, `quic hp`. |
| TLS 1.3 handshake | `Tls13ClientHandshake` | A message-level state machine with no I/O: it takes handshake bytes at an encryption level (`Initial`, `EarlyData`, `Handshake`, `Application`) and returns the bytes to send per level, the secrets installed per level and direction, and completion or a failure. QUIC drives it directly (RFC 9001 section 4.1); over TCP the record layer drives it. |
| TLS 1.3 records | `Tls13RecordLayer` | Content types, AEAD per-record nonces, the 2^14 limit, `change_cipher_spec` compatibility, alerts, KeyUpdate. |
| TLS 1.2/1.1/1.0 | `TlsPrf`, `Tls12RecordLayer`, `Tls12ClientHandshake` | RFC 5246 PRF (MD5+SHA-1 for 1.0/1.1), key block, CBC-HMAC with explicit IVs, encrypt-then-MAC, AEAD records, the 1/n-1 split; ECDHE, DHE, RSA, anonymous and SRP key exchange. |
| Resumption | `TlsSessionRecord`, `TlsSessionCodec` | Everything needed to resume a session (version, suite, ticket or session ID, secrets, ticket age add, lifetime, ALPN, SNI, maximum early data, time received). `TlsSessionCodec` writes and reads it as OpenSSL's `SSL_SESSION` DER encoding, so the opaque part of a `--ssl-sessions` file (curl's `vtls_spack.c` format, BL-710) is what the OpenSSL build writes and each build can read the other's file. |
| Extras | `OcspStapleVerifier`, `EchClientHello`, `SrpClient` | RFC 6960 checking of a stapled response against `TimeProvider`; RFC 9849 inner and outer hellos with HPKE; RFC 5054 SRP-6a. |
| Connection | `TlsClientConnection` | `ConnectAsync(Stream transport, TlsClientSettings settings, IServerCertificateVerifier verifier, CancellationToken)`: sends the hello, picks the TLS 1.3 or 1.2 path from the ServerHello, and returns a `TlsClientStream` (a `Stream` like `SslStream`'s, plus the negotiated version, suite, group, ALPN, peer chain, OCSP outcome and session records) or a `TlsHandshakeFailure`. A failure is returned, never thrown; only `OperationCanceledException` escapes. |

Every type holds the solution's gates (100% line and branch coverage, complexity at most
10, CRAP at most 30). RFC 8448's traces (sections 3, 4, 5 and 6) are the reference
tests for TLS 1.3; the RFC 5054 Appendix B vectors for SRP; an in-memory server built
from the same codecs, with fixed randoms, pins TLS 1.2 exchanges.

### The verification hand-off: one verifier

The hand-built client does not verify certificate chains. When the server's
`Certificate` message arrives (TLS 1.3, or TLS 1.2 `Certificate`, with the TLS 1.2
`CertificateStatus` if present), it calls

```csharp
public interface IServerCertificateVerifier
{
    ServerCertificateVerdict Verify(ServerCertificateChain presented);
}
```

where `ServerCertificateChain` carries the DER certificates in the order sent, the host
name the client offered in SNI (or the IP address), and the stapled OCSP response bytes
if any. `ServerCertificateVerdict` is either accepted or rejected with an opaque object
the client carries back untouched in its `TlsHandshakeFailure`. On rejection the client
sends the alert RFC 8446 section 6.2 prescribes (`bad_certificate` 42 unless the
verdict names another) and stops.

`Curl.Networking.UnitLibrary` implements the interface with the code that verifies for
`SslStream` today: BL-708 moves the body of `SslStreamTlsProvider`'s
`RemoteCertificateValidationCallback` (trust anchors from `ReadTrustAnchors`,
`WithoutChainErrorsCurlTolerates`, `WithTheNameCheckCurlRuns`,
`ObservePeerVerification`, `VerifyPeer`, `OpenSslVerifyResult`, `TlsFailureMessages`)
into one class that both providers call. For the hand-built path that class builds the
`X509Chain` with the same `X509ChainPolicy` and computes the `SslPolicyErrors` value
`SslStream` would have passed (`RemoteCertificateNotAvailable` for an empty chain,
`RemoteCertificateChainErrors` when the chain does not build,
`RemoteCertificateNameMismatch` from `X509Certificate2.MatchesHostname`). So `-k`,
`--cacert`, `--capath`, `--pinnedpubkey`, `--ssl-revoke-best-effort`, exit 60 and every
message stay exactly as they are on the `SslStream` path; there is no second verifier.

What the client does check itself is what is not chain verification: the
`CertificateVerify` signature (or TLS 1.2 `ServerKeyExchange` signature) against the
leaf's public key, the `Finished` MACs, and, for `--cert-status`, the stapled response
(`OcspStapleVerifier`, BL-705), whose outcome it reports so the caller fails with exit
91.

### Failures and text

`TlsHandshakeFailure` is typed: the alert sent or received (description, which side),
a verification rejection, or a local cause (no shared version, suite or group; a
decode error; the transport closed). `Curl.Networking.UnitLibrary` maps it to curl's exit
(35 for a handshake failure, 60 for verification, 91 for certificate status, and the
others BL-617 records) and message. Where the platform's curl has the same event, the
text is the platform's curl's (Schannel's wording on Windows, OpenSSL's on Linux and
macOS, ADR-0009, ADR-0085), so a user cannot tell which path ran. Where the platform's
curl cannot do the thing at all (Schannel with `--curves`), the text is the curl build
that does it on that platform: curl.se's official LibreSSL build on Windows, OpenSSL
elsewhere. BL-617 records each such message as measured.

## Consequences

- Every transfer that works today keeps using `SslStream`; the hand-built client only
  adds what was missing, and QUIC gets the TLS 1.3 it needs. No TLS feature any official
  curl build offers is refused on any platform.
- There are two TLS implementations to keep in step. The shared verifier, one routing
  function with data-row tests and the measured profiles keep the observable behaviour
  the same on both paths.
- Hand-written TLS is a security liability: it is held to RFC 8448's traces, negative
  cases for every alert, constant-time primitives (ADR-0118) and the quality gates, but
  has not had OpenSSL's scrutiny. The routing rule keeps it off every connection that
  does not need it.
- On Windows a server sees the Schannel hello from either path, and on Linux and macOS
  the OpenSSL hello, so a server's choices match the platform's curl.
- What the tasks rely on from this ADR:
  - **BL-696** creates `Curl.Tls.UnitLibrary` and its tests with the references and the
    `CLAUDE.md` rules above (BCL and `Curl.Cryptography.UnitLibrary`, no socket or
    `SslStream`, injected randomness and time, RFC 8448 as reference tests).
  - **BL-697** builds `Tls13KeySchedule`, `HkdfLabel` (public, for QUIC) and
    `TranscriptHash` for SHA-256 and SHA-384.
  - **BL-698** builds the codecs and the extension list in "What the client supports",
    and the three `ClientHelloProfile`s, pinned to the captured bytes above with the
    random, session ID and key shares injected.
  - **BL-699** builds `Tls13ClientHandshake` as the I/O-free state machine above, with
    the TLS 1.3 suites, groups and signature algorithms in the table, the
    `IServerCertificateVerifier` call, and `TlsHandshakeFailure`.
  - **BL-700** builds `Tls13RecordLayer`, `TlsClientStream` and `TlsClientConnection`
    for TLS 1.3 over a caller's `Stream`.
  - **BL-701** builds `TlsSessionRecord` and `TlsSessionCodec` (OpenSSL's `SSL_SESSION`
    DER), TLS 1.3 resumption and 0-RTT.
  - **BL-702** builds `TlsPrf` and `Tls12RecordLayer` for every TLS 1.2 and below bulk
    cipher in the table, with encrypt-then-MAC and the 1/n-1 split.
  - **BL-703** builds `Tls12ClientHandshake` with the key exchanges, suites, groups,
    signature algorithms and resumption in the table, and the same verifier call.
  - **BL-704** builds `SrpClient` and the three SRP suite families in the table.
  - **BL-705** builds `OcspStapleVerifier` and reports its outcome for exit 91.
  - **BL-706** builds `EchClientHello` to RFC 9849, including GREASE.
  - **BL-707** decodes HTTPS records (RFC 9460) so `--ech` gets its configuration
    through DoH; it relies only on this ADR's statement that the ECH configuration is a
    `TlsClientSettings` input.
  - **BL-708** adds the second `ITlsProvider` in `Curl.Networking.UnitLibrary`, the
    routing function with the rows above, the shared verifier class moved out of
    `SslStreamTlsProvider`, the profile choice per platform, and the failure mapping.
  - BL-783, BL-784, BL-785 and BL-786 are filed by this decision for the primitives and
    the certificate decompression the table needs.
- BL-617 adds the per-option detail and any new rows; BL-724 (QUIC) relies on the
  I/O-free handshake and on `HkdfLabel`; BL-610, BL-709 to BL-714 each wire one row.

## Alternatives considered

- **Use the hand-built client for every TLS connection.** One implementation, but every
  transfer that works today would move onto new, less-scrutinised code, and on Windows
  the `-v` lines and failures would stop coming from Schannel itself. Rejected: the
  hand-built client carries only what `SslStream` cannot.
- **One ClientHello profile for every platform (OpenSSL's).** Simpler, but on Windows a
  server would see an OpenSSL hello from what claims to be the Schannel build, and could
  choose differently (Schannel does not offer ChaCha20 in TLS 1.3). Rejected for the
  measured profile of the curl being replaced.
- **A second chain verifier inside `Curl.Tls.UnitLibrary`.** Would duplicate curl's
  tolerated chain errors, name checks and messages, and drift. Rejected for the single
  verifier behind `IServerCertificateVerifier`.
- **Refuse the options on platforms whose `SslStream` cannot do them.** Contrary to
  Stewart's standing rule (2026-09-28) that what any official curl build does, Curl does
  everywhere. Rejected.
- **A package (BouncyCastle's TLS, a native OpenSSL binding).** Forbidden by the
  BCL-only rule and a trim and native-AOT risk. Rejected.
