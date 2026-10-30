---
id: BL-4282
title: Extension CoAP: offer extended_master_secret in the DTLS 1.2 handshake
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4251]
touches: [Curl.Extension.Coap.UnitLibrary/Dtls/Handshake/DtlsClientHandshake.cs, Curl.Extension.Coap.UnitLibrary/Dtls/Handshake/DtlsClientHello.cs, Curl.Extension.Coap.UnitTests/Dtls/Handshake, Curl.Extension.Coap.UnitTests/Dtls/TestServer/DtlsTestServer.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4282 — Extension CoAP: offer extended_master_secret in the DTLS 1.2 handshake

## Goal

The CoAP extension's DTLS 1.2 client offers extended_master_secret (RFC 7627) and, when the server echoes it, derives the master secret from the session hash.

## Context

ADR-1078 (BL-4251) left it out because `Dtls/TestServer/DtlsTestServer` ignores the extension, so the extended derivation could not be tested. Add `ExtendedMasterSecretExtension.Encode()` to `DtlsClientHello.ExtensionsFor`, read it from the ServerHello in `DtlsClientHandshake.ExchangeKeysAsync`, and use `TlsPrf.Sha256.ComputeExtendedMasterSecret` over SHA-256 of the transcript through ClientKeyExchange (RFC 7627 section 4). Give the test server an option to echo it.

## Acceptance criteria

- [x] Unit tests run a full handshake against a test server that echoes extended_master_secret and one that does not, both completing.
- [x] ADR-1078's extended_master_secret line says what the code now does.
- [x] Curl.Extension.Coap.UnitLibrary meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Master secret now derived after ClientKeyExchange joins the transcript (moved from FinishAsync). Quality: 0 failing members.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 80613338 54477d71 cf4c1338 3d836d5e.
- 2026-10-29: Doing -> Done. DTLS client offers extended_master_secret and derives the extended master secret when echoed
