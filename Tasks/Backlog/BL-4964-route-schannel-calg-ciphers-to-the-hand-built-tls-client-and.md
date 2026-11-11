---
id: BL-4964
title: Route Schannel CALG_* --ciphers to the hand-built TLS client and apply the suite filter
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4963]
touches: [Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs, Curl.Networking.UnitLibrary/TlsClientRouting.cs, Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitTests/TlsClientRoutingTests.cs, Documentation/Decisions]
requirement: none
created: 2026-11-11
completed:
---
# BL-4964 — Route Schannel CALG_* --ciphers to the hand-built TLS client and apply the suite filter

## Goal

`--ciphers CALG_...` on the Schannel build reaches the hand-built TLS client, which offers only the filtered suites, so `CALG_AES_128` negotiates `ECDHE-RSA-AES128-GCM-SHA256` and `CALG_RSA_KEYX` exits 35 with the REF message.

## Context

Split from BL-4911 (STD-008 of the 2026-11-11 outside fitness review; REF = curl 8.22.0 Schannel). `SchannelAlgorithmList.cs` only validates a `CALG_*` list; the ALG_IDs are never used to limit offered suites. SslStream cannot restrict suites on Windows, so the list must route to the hand-built client (`TlsClientRouting.cs`, `HandBuiltTlsProvider.cs` ~line 128, `SslStreamTlsProvider.cs` ~line 148).

## Acceptance criteria

- [ ] `TlsClientRouting.ReasonInBuild` routes the Schannel build with a `--ciphers` list to the hand-built client, and `HandBuiltTlsProvider.SelectCipherSuites` uses `SchannelAlgorithmList.OfferedSuites`; `TlsClientRoutingTests` cover the new row.
- [ ] Against a local TLS 1.2 server, `--ciphers CALG_AES_128` negotiates `ECDHE-RSA-AES128-GCM-SHA256`, and `--ciphers CALG_RSA_KEYX` exits 35 with `schannel: next InitializeSecurityContext failed: SEC_E_ILLEGAL_MESSAGE (0x80090326) - ...` as curl 8.22.0 prints (measured, not assumed; stand-in in PowerShell or a C# file-based app, never Python).
- [ ] An ADR (stashed draft ADR-1238; renumber if taken) records the decision, "Decided by Claude under Stewart's delegation"; `dotnet build` is clean and the fast tests pass.

## Notes

Split from BL-4911.

Continues the stashed work of BL-4911.

Lane 6 wrote the routing, HandBuiltTlsProvider change and ADR-1238 (unbuilt) in the stash. Needs BL-4963 first.

## Log

- 2026-11-11: Created.
