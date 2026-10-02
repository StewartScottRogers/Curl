# ADR-0350 — The OpenSSL build's TLS 1.3 hello pads and drops `ec_point_formats` as measured

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1048.
Follows ADR-0284's Consequences.

## Context

BL-709's captures (Ubuntu curl 8.18.0, OpenSSL 3.5.5, ADR-0284) show three differences
between the OpenSSL build's ClientHello under `--curves` and the hand-built client's:

- `--curves X25519` and `P-384:X25519` add `padding` (0x0015) after every other extension.
  OpenSSL's `tls_construct_ctos_padding` (on through `SSL_OP_ALL`, which curl sets) pads a
  hello of 256 to 511 bytes, handshake header included, up to 512. The default hello, with
  its ML-KEM key share, is far over 512 and is never padded.
- `--curves X25519MLKEM768` leaves out `ec_point_formats` (0x000b): OpenSSL sends it only
  while a group TLS 1.2 can agree ECDHE on remains.
- `--curves brainpoolP256r1:X25519` keeps 001a beside 001d in a hello that also offers TLS
  1.3; the hand-built client already did (BL-1086).

## Decision

- `ClientHelloProfile.PadsTcpHello` is set on the OpenSSL profile. `HandBuiltTlsProvider`
  then puts `padding` last in the TLS 1.3 hello's order, after `early_data` and before
  `encrypted_client_hello`, and `Tls13ClientHelloBuilder`'s existing rule (256 to 511 bytes
  up to 512, the PSK binder counted) decides whether it is sent.
- `ClientHelloProfileMapping` sends `ec_point_formats` in both the TLS 1.3 and the TLS 1.2
  hello only while `Tls12SupportedGroups` is not empty.

Not measured, so left as they were: the TLS 1.2-ceiling hello (`--tls-max 1.2`) is not padded
(BL-941 measured its order without `padding`); the QUIC hello, built from the profile's
`ExtensionOrder`, is not padded; `encrypted_client_hello` stays last, as the ECH tests pin
(OpenSSL 3.5 sends no ECH).

## Consequences

The three measured `--curves` hellos match OpenSSL's extension order byte for byte
(`HandBuiltTlsProviderTests.AuthenticateAsClientAsync_WithCurvesInTheOpenSslBuild_SendsTheMeasuredExtensionsAndGroups`).
Any other OpenSSL-build hello of 256 to 511 bytes, such as a HelloRetryRequest's second
hello, is padded too, as OpenSSL pads it.

## Alternatives considered

- Adding `padding` to the profile's `ExtensionOrder`: QUIC's hello reads that order too, and
  `ClientHelloProfile.Build` has no encoder for it; a flag keeps QUIC unchanged.
- Padding after `encrypted_client_hello`: unmeasured either way; keeping ECH last keeps the
  pinned ECH behaviour.
