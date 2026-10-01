# ADR-0290 — QUIC handshakes read --ciphers and --cert as OpenSSL-API builds and send the OpenSSL profile's TLS 1.3 hello

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-847;
recorded by BL-956, since BL-847 could not write to this folder while BL-911 held it.

## Context

BL-847 made QUIC handshakes honour `--ciphers`, `--tls13-ciphers` and `--cert`, and gave the
OpenSSL build its own QUIC ClientHello. Over TCP the Windows reference build is Schannel's,
which refuses `--ciphers` and reads `--cert` as a certificate store path. But no Schannel
curl speaks QUIC: curl.se's Windows build dials HTTP/3 through ngtcp2 on LibreSSL, and the
Linux and macOS builds through ngtcp2 on OpenSSL. Both QUIC builds therefore run on the
OpenSSL API. ADR-0140 already fixed which hello each build sends; it left open what the
OpenSSL build's QUIC hello holds.

## Decision

1. **Both QUIC builds read the options as OpenSSL-API builds.** `QuicDialer.PrepareTls`
   reads `--ciphers`/`--tls13-ciphers` through `OpenSslCipherSuites` and `--cert` through
   `ClientCertificateLoader.LoadAsOpenSslBuild`, on Windows as elsewhere; the Windows build
   keeps a drive letter's colon in `--cert`, as curl's Windows builds do. So the Windows
   build does not refuse `--ciphers` over QUIC as the Schannel build does over TCP.
   `--ssl-auto-client-cert` is a Schannel option and does not reach QUIC.
2. **The offered suites are cut to those QUIC can protect.** The selection is filtered by
   `QuicPacketProtection.CanProtect` (RFC 9001 section 5.3 rules out the CCM_8 suite); a
   selection left empty fails with exit 59 (`CURLE_SSL_CIPHER`) and
   `OpenSslCipherSuites.Unapplied`'s text, the one `HandBuiltTlsProvider` gives over TCP.
3. **The OpenSSL build's QUIC hello is `ClientHelloProfile.OpenSsl`'s TLS 1.3 parts.**
   `QuicClientSettings.CreateOpenSslTlsSettings` keeps its TLS 1.3 suites QUIC can protect,
   all its groups and the X25519MLKEM768 + X25519 key shares, the signature schemes the
   client can check (as ADR-0235 cuts them over TCP), `psk_key_exchange_modes` and
   `compress_certificate`, in the profile's order. It leaves out `renegotiation_info`,
   `ec_point_formats`, `encrypt_then_mac` and `extended_master_secret` (TLS 1.2 and below
   only) and `post_handshake_auth` (RFC 9001 section 4.4 forbids it to a QUIC client); puts
   `quic_transport_parameters` last, where OpenSSL's extension table puts it; offers ALPN
   `h3` then `h3-29`; and sends no legacy session ID. This hello is derived, not measured:
   BL-957 captures a real OpenSSL build's QUIC Initial and replaces it with the measured one.

## Consequences

- `--ciphers`, `--tls13-ciphers` and `--cert` behave the same over HTTP/3 on every
  platform, and differ from the Windows TCP behaviour exactly where the real Windows builds
  differ.
- The `--cert` certificate is disposed when the QUIC attempt ends; QUIC forbids
  post-handshake authentication, so nothing needs it later.
- Until BL-957 lands, a byte-level difference between this hello and real OpenSSL curl's is
  possible; the tests in `TcpConnectorQuicTests.TlsOptions.cs` pin the derived hello so the
  measured one shows up as a deliberate change.

## Options considered

- **Treat the Windows QUIC build as Schannel's** (refuse `--ciphers`, read `--cert` as a
  store path): rejected; no Schannel curl dials QUIC, so this would match no real curl.
- **Send the full `ClientHelloProfile.OpenSsl` over QUIC**: rejected; its TLS 1.2-only
  extensions and `post_handshake_auth` are not sent by a TLS 1.3-only QUIC client.
- **Offer CCM_8 and fail later**: rejected; QUIC cannot protect packets with it, so offering
  it would let a server pick a suite the connection cannot use.
