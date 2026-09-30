# ADR-0284 — `--curves` and `--sigalgs` read OpenSSL 3.5's lists on every platform and fail as the build that applies them

- **Status:** Accepted
- **Date:** 2026-09-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-709.
Carries out ADR-0151's `--curves` and `--sigalgs` rows.

## Context

ADR-0151 routes `--curves` and `--sigalgs` to the hand-built TLS client on every platform
and leaves BL-709 to pin the text of a handshake that shares nothing. The Schannel build
ignores both options; curl.se's Windows build (LibreSSL 4.2.1) applies `--curves` only; the
OpenSSL build applies both.

Measured 2026-09-30. ClientHellos were captured with `Record-CurlExchange.ps1` in plain TCP
mode (the ClientHello lands in `request.bin`) from Ubuntu's curl 8.18.0 with OpenSSL 3.5.5
under WSL; failures against `openssl s_server` (RSA certificate, `-groups P-384` where
stated), from that build and from curl.se's 8.18.0.

| `--curves` value (OpenSSL build) | `supported_groups` | `key_share` |
| --- | --- | --- |
| none | 11ec 001d 0017 001e 0018 0019 0100 0101 | 11ec 001d |
| `X25519` | 001d | 001d |
| `P-384:X25519` | 0018 001d | 0018 |
| `X25519MLKEM768` | 11ec | 11ec |
| `*P-256:X25519` / `P-256:*X25519` | 0017 001d | 0017 / 001d |
| `X25519/P-256` | 001d 0017 | 001d |
| `*P-256:*X25519:P-384` | 0017 001d 0018 | 0017 001d |
| `?bogus:X25519`, `X25519:X25519` | 001d | 001d |
| `DEFAULT:-X25519MLKEM768` | 001d 0017 001e 0018 0019 0100 0101 | 001d |
| `X25519:P-256:-X25519` | 0017 | 0017 |
| `brainpoolP256r1:X25519` | 001a 001d | 001d |
| `ffdhe2048:X25519` | 0100 001d | 0100 |
| `X25519`, `--tls-max 1.2` | 001d | (none) |

Names match without regard to case (`x25519`, `p-256`). `bogus`, `X25519:bogus`,
`X25519:`, `:X25519`, `X25519::P-256`, `x25519,P-256` and `*DEFAULT` fail
`curl: (59) failed setting curves list: '<value>'`; `?bogus` and `-X25519` fail
`curl: (35) TLS connect error: error:0A000127:SSL routines::no suitable groups`. curl.se's
LibreSSL build refuses `?bogus`, `-X25519` and `X25519MLKEM768` with the same exit 59 line.

| `--sigalgs` value (OpenSSL build) | `signature_algorithms` |
| --- | --- |
| `ECDSA+SHA256` | 0403 (also with `--tls-max 1.2`) |
| `rsa_pss_rsae_sha256:ECDSA+SHA256` | 0804 0403 |
| `RSA+SHA256:RSA+SHA256` | 0401 |
| `RSA+SHA256:RSA-PSS+SHA256:PSS+SHA384:ECDSA+SHA384:ed25519:ed448:DSA+SHA256:RSA+SHA1:ECDSA+SHA1:RSA+SHA224` | 0401 0804 0805 0503 0807 0808 0402 0301 |
| `rsa_pss_pss_sha256:ecdsa_brainpoolP256r1tls13_sha256:mldsa65:rsa_pkcs1_sha512` | 0809 081a 0905 0601 |

`bogus`, `RSA+SHA256:`, `RSA+SHA256:bogus` and `RSA+SHA256,ECDSA+SHA256` fail
`curl: (59) failed setting signature algorithms: '<value>'`; `RSA+SHA1` alone fails
`curl: (35) TLS connect error: error:0A000076:SSL routines::no suitable signature algorithm`.
With both options bogus the curves line is printed; `--curves ?bogus --sigalgs bogus` prints
the sigalgs line, and `--curves ?bogus --sigalgs RSA+SHA1` the no-suitable-groups line.

Against a server sharing nothing (`-groups P-384` with `--curves X25519`, or an RSA-only
server with `--sigalgs ECDSA+SHA256`), the server sends `handshake_failure`: the OpenSSL
build prints `curl: (35) TLS connect error: error:0A000410:SSL routines::ssl/tls alert
handshake failure` for both, curl.se's build `curl: (35) TLS connect error:
error:14004410:SSL routines:CONNECT_CR_SRVR_HELLO:sslv3 alert handshake failure` for
`--curves` (it ignores `--sigalgs`).

## Decision

1. **One syntax everywhere: OpenSSL 3.5's.** `OpenSslGroupList` reads `--curves`: `:` or
   `/` separators, `?` (skip an unknown name), `*` (key share), `-` (remove), `DEFAULT` (the
   platform profile's groups and key shares, the Schannel profile's on Windows). Without
   `*`, the first group the client can share a key for gets the one key share.
   `OpenSslSignatureAlgorithmList` reads `--sigalgs`: `:` separators, `SIG+HASH` pairs and
   scheme names, SHA-1 schemes dropped as OpenSSL's default security level drops them. It is
   a superset of LibreSSL's plain lists, so on Windows the extended markers are accepted
   where curl.se's build refuses them; nothing that build accepts is refused.
2. **The lists replace only the profile's lists.** `CurvesAndSignatureAlgorithms.Apply`
   swaps `supported_groups`, `key_share` and `signature_algorithms` into the platform
   curl's profile; extension order and every other extension stay the profile's.
3. **Groups and schemes the client cannot run are left out, as unrunnable cipher suites
   are (ADR-0011).** OpenSSL names for groups `Curl.Tls` has no key exchange for
   (`SecP256r1MLKEM768`, `SecP384r1MLKEM1024`, `MLKEM512/768/1024`, the brainpool TLS 1.3
   groups) are known names, dropped rather than refused, and schemes the client cannot check
   (ML-DSA, ed448, brainpool TLS 1.3 ECDSA) are dropped. A list left with no group fails
   with the no-suitable-groups line, one left with no scheme with the no-suitable-signature
   line, both exit 35. TLS 1.3's `supported_groups` carries only groups it can share; the
   brainpool curves are offered to TLS 1.2 only.
4. **Failures in the OpenSSL build's order and text on every platform**: curves exit 59,
   sigalgs exit 59, no group 35, no scheme 35, all before a byte is sent.
5. **A failed handshake's text is the applying build's.** Off Windows, the OpenSSL build's
   as ever. On Windows with `--sigalgs`, the OpenSSL build's text for every handshake
   failure (only it applies the option, ADR-0151). On Windows with `--curves` alone, a
   `handshake_failure` alert from the server prints curl.se's measured line; any other
   failure keeps the Schannel build's text, since no other curl.se failure was measured.

## Consequences

- Each ClientHello list is pinned per value in `HandBuiltTlsProviderTests.CurvesAndSignatureAlgorithms`,
  `OpenSslGroupListTests` and `OpenSslSignatureAlgorithmListTests`, in both builds' profiles.
- Where the client cannot offer what OpenSSL offers, the bytes differ: the dropped groups
  and schemes above; `ec_point_formats`, which OpenSSL leaves out when no EC group remains;
  the `padding` extension OpenSSL adds when a shorter list brings the ClientHello under 512
  bytes; and brainpool groups in a hello that also offers TLS 1.3. BL-1049 (the groups),
  BL-1047 (the signature schemes) and BL-1048 (the extensions) cover each.
- `--curves ?bogus` on Windows fails with exit 35 where curl.se's build fails with 59; both
  are failures, and the OpenSSL syntax is the one scripts written for Linux use.

## Alternatives considered

- **LibreSSL's syntax on Windows.** Closer to curl.se's build for the markers, but two
  parsers for one option, and the Schannel build, the Windows reference, accepts any value.
- **Refuse names for groups the client cannot run.** Simpler, but turns a list OpenSSL
  accepts, such as `SecP256r1MLKEM768:X25519`, into exit 59 where OpenSSL connects.
- **Offer unrunnable groups and schemes anyway.** Byte-identical to OpenSSL, but a server
  choosing one would fail a handshake OpenSSL completes.
