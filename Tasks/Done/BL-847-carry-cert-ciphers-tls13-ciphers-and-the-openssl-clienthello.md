---
id: BL-847
title: Carry --cert, --ciphers, --tls13-ciphers and the OpenSSL ClientHello profile into QUIC handshakes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-728, BL-787]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Quic.UnitLibrary, Curl.Quic.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-847 — Carry --cert, --ciphers, --tls13-ciphers and the OpenSSL ClientHello profile into QUIC handshakes

## Goal

A QUIC handshake started by `QuicDialer` presents the `--cert` client certificate, offers the suites `--ciphers`/`--tls13-ciphers` select as `HandBuiltTlsProvider` does, and sends the OpenSSL build's TLS 1.3 ClientHello on Linux and macOS (the LibreSSL one on Windows), as ADR-0144 section 5 decides.

## Context

- BL-728 (ADR-0180) dials QUIC with `QuicClientSettings.CreateCurlTlsSettings`, curl.se's LibreSSL profile, on every platform, and passes none of `TlsClientOptions`' client-certificate or cipher options into the handshake.
- `HandBuiltTlsProvider` already loads `--cert` (`ClientCertificateLoader.Load`, `ToTlsClientCertificate`) and selects suites (`SelectCipherSuites`, `OpenSslCipherSuites`); reuse them. BL-787 builds the measured ClientHello profiles in `Curl.Tls.UnitLibrary`.
- Code: `Curl.Networking.UnitLibrary/QuicDialer.cs` (`AttemptAsync`), `Curl.Quic.UnitLibrary/QuicClientSettings.cs`.

## Acceptance criteria

- [x] `Curl.Networking.UnitTests` show a QUIC handshake against the in-memory server presenting a `--cert` certificate when the server asks for one, offering only the suites `--tls13-ciphers` names in the OpenSSL build, and failing with exit 59 and `HandBuiltTlsProvider`'s message when none can be offered.
- [x] A test pins the ClientHello a QUIC handshake sends in the OpenSSL build against the OpenSSL profile BL-787 measured.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary` and `Curl.Quic.UnitLibrary`.

## Notes

Filed by BL-728.

- Plan (small enough to write directly against ADR-0140 and ADR-0144 section 5, no separate architect pass): `QuicDialer.PrepareTls` selects suites and loads `--cert` before the trust event, in `HandBuiltTlsProvider`'s order, then takes the build's QUIC hello settings `with` the suites and the client certificate. `QuicClientSettings.CreateCurlTlsSettings` is renamed `CreateLibreSslTlsSettings` (there are two curls' hellos now) and `CreateOpenSslTlsSettings` is added; BL-824's text follows the rename.
- Decision (by Claude under Stewart's delegation): both QUIC builds are OpenSSL-API builds (curl.se's LibreSSL build on Windows, OpenSSL elsewhere), so both read `--ciphers`/`--tls13-ciphers` through `OpenSslCipherSuites.Select` and `--cert` through the new `ClientCertificateLoader.LoadAsOpenSslBuild(options, recognisesDriveLetters)`; the Windows build keeps a drive letter's colon, as curl's Windows builds do. The Windows build therefore does not refuse `--ciphers` over QUIC, unlike the Schannel build over TCP. `--ssl-auto-client-cert` is a Schannel option and does not reach QUIC.
- Decision: offered suites are cut to `QuicPacketProtection.CanProtect` (RFC 9001 section 5.3 rules out CCM_8); a selection left empty is exit 59 with `OpenSslCipherSuites.Unapplied`'s text, the one `HandBuiltTlsProvider` gives.
- Decision: the OpenSSL QUIC hello is `ClientHelloProfile.OpenSsl`'s TLS 1.3 parts per ADR-0140: TLS 1.3 suites, all groups and the X25519MLKEM768 + X25519 shares, signature schemes cut to those the client can check (as ADR-0222 cuts them over TCP), `psk_key_exchange_modes` and `compress_certificate`; `renegotiation_info`, `ec_point_formats`, `encrypt_then_mac`, `extended_master_secret` (TLS 1.2-only) and `post_handshake_auth` (RFC 9001 section 4.4) left out; `quic_transport_parameters` last, where OpenSSL's extension table puts it; no legacy session ID. Not measured - this lane runs on Windows - so BL-943 captures a real Initial.
- The ADR could not be written here: BL-911, in Doing, holds `Documentation/Planning/Decisions`. The decisions above are the record until BL-942 writes the ADR; code comments cite BL-847 meanwhile.
- `QuicDialer.AttemptAsync` reached complexity 12 once the preparation step was added, so the trust, channel and handshake steps moved to `ConnectAsync`. The `--cert` certificate is disposed when the attempt ends; QUIC forbids post-handshake authentication, so nothing needs it later.
- Tests: `TcpConnectorQuicTests.TlsOptions.cs` (9: `--cert` presented in both builds, exit 58, `--tls13-ciphers` offered alone, Windows `--ciphers`, exit 59 for an unknown name and for CCM_8 only, the OpenSSL hello pinned against `ClientHelloProfile.OpenSsl`, the LibreSSL order); `QuicClientSettingsTests` (3). The Networking copy of the in-memory QUIC server can now send a CertificateRequest and reads the client's Handshake flight in pieces.
- Gates: `dotnet build Curl.slnx -warnaserror` clean; fast tests all green (Networking 1561, Quic 405); `Measure-CodeQuality.ps1` 100% line and branch, 0 failing members for `Curl.Networking.UnitLibrary` and `Curl.Quic.UnitLibrary`.
- Follow-ups filed: BL-942 (the ADR), BL-943 (measure the OpenSSL build's QUIC Initial).

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. QUIC handshakes present --cert, offer the --ciphers/--tls13-ciphers suites QUIC can protect (exit 59 when none), and send the OpenSSL profile's TLS 1.3 ClientHello in the OpenSSL build
