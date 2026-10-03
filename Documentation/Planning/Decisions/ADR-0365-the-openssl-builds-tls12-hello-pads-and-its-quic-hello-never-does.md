# ADR-0365 — The OpenSSL build's TLS 1.2-ceiling hello pads, and its QUIC hello never does

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1156.
Follows ADR-0350, which left both hellos unpadded because neither was measured at 256 to 511
bytes.

## Context

Measured 2026-10-02 in WSL with OpenSSL 3.5.5:

- Ubuntu's curl 8.18.0, `--tls-max 1.2 --curves X25519`, through `Record-CurlExchange.ps1` in
  plain TCP mode. To an IP literal (no `server_name`) the hello is 192 bytes and carries
  `ff01 000b 000a 0010 0016 0017 000d`, no `padding`. To a 75-character host name (through
  `--resolve`) it would be 272 bytes, and is sent as 512: `ff01 0000 000b 000a 0010 0016
  0017 000d 0015`, `padding` last with 232 zero bytes. The TLS 1.3 hello's rule (ADR-0350)
  holds for the TLS 1.2 hello too.
- No Ubuntu curl speaks HTTP/3 (`--http3-only` is exit 2 there), so OpenSSL 3.5.5's own QUIC
  client stands in: `openssl s_client -quic -alpn h3 -bugs -groups X25519 -servername <75
  characters> -msg`. `-bugs` sets `SSL_OP_ALL`, which curl sets and which turns padding on.
  The QUIC hello is 335 bytes and carries no `padding`; the same command without `-quic`
  sends a 512-byte hello ending in `padding`. OpenSSL never pads a QUIC hello.

## Decision

- `Tls12ClientSettings.PadHello` pads a hello of 256 to 511 bytes to 512 with `padding`
  last, by the rule now shared as `PaddingExtension.DataLengthFor`.
  `HandBuiltTlsProvider` sets it from `ClientHelloProfile.PadsTcpHello`, so only the
  OpenSSL build pads.
- The TLS 1.2 half of a hello that also offers TLS 1.3 is built unpadded
  (`Tls12ClientHelloBuilder.BuildUnpadded`); the TLS 1.3 order places the one `padding`.
- The QUIC hello stays unpadded; `QuicClientSettingsTests` pins it at 256 to 511 bytes.

## Consequences

`HandBuiltTlsProviderTests.AuthenticateAsClientAsync_WithCurvesUnderATls12CeilingInTheOpenSslBuild_SendsTheMeasuredExtensions`
pins both measured TLS 1.2 hellos. A resumed TLS 1.2 hello whose ticket brings it into the
range is padded too, as OpenSSL pads it.

## Alternatives considered

- Leaving the TLS 1.2 hello unpadded: measured wrong for a long host name.
- Measuring QUIC with a curl built against OpenSSL's QUIC stack: none is packaged for Ubuntu,
  and the TLS extensions come from the same `tls_construct_ctos_padding` either way, so the
  library's own QUIC client answers the question.
