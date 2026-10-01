---
id: BL-956
title: Record BL-847's QUIC ClientHello, cipher and --cert decisions in an ADR
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-847]
touches: [Documentation/Planning/Decisions, Curl.Networking.UnitLibrary, Curl.Quic.UnitLibrary, Curl.Networking.UnitTests, Curl.Quic.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-956 — Record BL-847's QUIC ClientHello, cipher and --cert decisions in an ADR

## Goal

An ADR marked "Decided by Claude under Stewart's delegation" records the three decisions BL-847 made for QUIC handshakes, and the code comments that cite `BL-847` for them cite that ADR instead.

## Context

- BL-847 could not add the ADR itself: BL-911 held `Documentation/Planning/Decisions` in its `touches` at the time. The decisions are in BL-847's Notes (in `Tasks/Done` or its archive).
- The three decisions: (1) both QUIC builds - curl.se's LibreSSL build on Windows and the OpenSSL build elsewhere - read `--ciphers`/`--tls13-ciphers` through `OpenSslCipherSuites` and `--cert` through `ClientCertificateLoader.LoadAsOpenSslBuild`, so the Windows build does not refuse `--ciphers` over QUIC as the Schannel build does over TCP; (2) the offered suites are cut to those `QuicPacketProtection.CanProtect` accepts, and none left is exit 59 with `OpenSslCipherSuites.Unapplied`'s text; (3) `QuicClientSettings.CreateOpenSslTlsSettings` is `ClientHelloProfile.OpenSsl` without `renegotiation_info`, `ec_point_formats`, `encrypt_then_mac`, `extended_master_secret` and `post_handshake_auth`, with `quic_transport_parameters` last, until BL-957 measures a real Initial.
- Code: `Curl.Networking.UnitLibrary/QuicDialer.cs` (`PrepareTls`), `ClientCertificateLoader.cs`, `Curl.Quic.UnitLibrary/QuicClientSettings.cs`.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions` holds an ADR for the three decisions, marked "Decided by Claude under Stewart's delegation", listed in the folder's `README.md` index.
- [ ] `grep -rn "BL-847" Curl.Networking.UnitLibrary Curl.Quic.UnitLibrary --include=*.cs` names that ADR beside or in place of each `BL-847` citation.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

Filed by BL-847.

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
