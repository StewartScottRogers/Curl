# ADR-0291 — The OpenSSL build's QUIC hello is measured: transport parameters first and the profile's TLS 1.2 extensions kept

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-957.
Supersedes ADR-0290 decision 3's derived hello; ADR-0290's other decisions stand.

## Context

ADR-0290 derived the OpenSSL build's QUIC ClientHello from `ClientHelloProfile.OpenSsl`
(Ubuntu's curl 8.18.0 over TCP, BL-787) and left BL-957 to measure a real one. Ubuntu's
curl has no HTTP/3, so no Ubuntu capture is possible. Fedora's curl 8.18.0 is built with
OpenSSL 3.5.7, ngtcp2 1.22.1 and nghttp3 1.18.0, and runs in Docker on the lane's machine.

BL-957 ran it as `curl --http3-only https://host.docker.internal:4433/` against
`Record-CurlExchange.ps1 -NoServer -UdpSink -Port 4433 -ListenAddress 0.0.0.0`, removed
the Initial protection of the captured datagrams (RFC 9001 section 5.2) with
`QuicPacketProtection.CreateClientInitial` and reassembled the CRYPTO frames (ngtcp2
scatters them across two Initials and out of order) into the 1,539-byte ClientHello. It
also recorded the same container's TCP hello (`curl https://host.docker.internal:4434/`
against the HTTP loopback server), so the QUIC hello could be compared with the TCP hello
of the same build and crypto policy.

Measured, QUIC against TCP for the same build:

| | TCP hello | QUIC hello |
| --- | --- | --- |
| Extension order | `ff01 0000 000b 000a 0010 0016 0017 0031 000d 002b 002d 0033 001b` | `0039 0000 000b 000a 0010 0016 0017 0031 000d 002b 002d 0033 001b` |
| `supported_versions` | `0304 0303` | `0304` |
| `signature_algorithms` | 19 schemes ending `0303 0301` | the same 17 without `0303 0301`; ML-DSA kept |
| Suites | TLS 1.3 then TLS 1.2 | the TLS 1.3 ones only |
| `session_id` | 32 bytes | empty |

Groups, key shares (X25519MLKEM768 and X25519), `ec_point_formats` (`00 01 02`),
`psk_key_exchange_modes` and `compress_certificate` are identical between the two. Fedora's
crypto policy sets its own groups, suites and signature list, which differ from Ubuntu's;
the extension order is the same as Ubuntu's TCP hello.

## Decision

`QuicClientSettings.CreateOpenSslTlsSettings` applies the measured TCP-to-QUIC change to
`ClientHelloProfile.OpenSsl`:

1. **Order:** `quic_transport_parameters` first, then the profile's order without
   `renegotiation_info`. `ec_point_formats`, `encrypt_then_mac`, `extended_master_secret`
   and `post_handshake_auth` stay, as OpenSSL sends them; ADR-0290's expectation that a
   TLS 1.3-only hello drops them was wrong.
2. **Lists:** the profile's groups, key shares, `ec_point_formats`, `psk_key_exchange_modes`
   and `compress_certificate` unchanged; its TLS 1.3 suites QUIC can protect; its signature
   schemes cut to those OpenSSL keeps in TLS 1.3 - the TLS 1.3 schemes (ML-DSA included)
   and RSA PKCS #1 v1.5 over SHA-2 (RFC 8446 section 4.2.3) - dropping SHA-1, SHA-224 and
   DSA. `supported_versions` is TLS 1.3 only, as the TLS 1.3 client always sends.
3. **The lists stay Ubuntu's,** not Fedora's: `ClientHelloProfile.OpenSsl` is the Linux
   reference hello over TCP, and the QUIC hello of the same OpenSSL differs from its TCP
   hello only in the ways the table shows.

## Consequences

- The Linux and macOS QUIC hello matches real OpenSSL curl's in extension order and
  presence; `QuicClientSettingsTests` pins the captured order, fixed extensions and the
  signature cut against the capture.
- A server that would act on `post_handshake_auth` over QUIC is not a concern: RFC 9001
  section 4.4 forbids it a post-handshake CertificateRequest, and real curl offers it too.

## Options considered

- **Keep ADR-0290's derived hello:** rejected; it differs from the measured one in five
  extensions and their order.
- **Take Fedora's lists as well:** rejected; they come from Fedora's crypto policy, not
  from OpenSSL, and the TCP reference is Ubuntu's.
