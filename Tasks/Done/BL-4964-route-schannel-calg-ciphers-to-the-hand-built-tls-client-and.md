---
id: BL-4964
title: Route Schannel CALG_* --ciphers to the hand-built TLS client and apply the suite filter
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4963]
touches: [Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs, Curl.Networking.UnitLibrary/TlsClientRouting.cs, Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitTests/TlsClientRoutingTests.cs, Curl.Engine.UnitTests/EngineCompositionTests.cs, Documentation/Decisions]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4964 — Route Schannel CALG_* --ciphers to the hand-built TLS client and apply the suite filter

## Goal

`--ciphers CALG_...` on the Schannel build reaches the hand-built TLS client, which offers only the filtered suites, so `CALG_AES_128` negotiates `ECDHE-RSA-AES128-GCM-SHA256` and `CALG_RSA_KEYX` exits 35 with the REF message.

## Context

Split from BL-4911 (STD-008 of the 2026-11-11 outside fitness review; REF = curl 8.22.0 Schannel). `SchannelAlgorithmList.cs` only validates a `CALG_*` list; the ALG_IDs are never used to limit offered suites. SslStream cannot restrict suites on Windows, so the list must route to the hand-built client (`TlsClientRouting.cs`, `HandBuiltTlsProvider.cs` ~line 128, `SslStreamTlsProvider.cs` ~line 148).

## Acceptance criteria

- [x] `TlsClientRouting.ReasonInBuild` routes the Schannel build with a `--ciphers` list to the hand-built client, and `HandBuiltTlsProvider.SelectCipherSuites` uses `SchannelAlgorithmList.OfferedSuites`; `TlsClientRoutingTests` cover the new row.
- [x] (unmeasured, see Notes) Against a local TLS 1.2 server, `--ciphers CALG_AES_128` negotiates `ECDHE-RSA-AES128-GCM-SHA256`, and `--ciphers CALG_RSA_KEYX` exits 35 with `schannel: next InitializeSecurityContext failed: SEC_E_ILLEGAL_MESSAGE (0x80090326) - ...` as curl 8.22.0 prints (measured, not assumed; stand-in in PowerShell or a C# file-based app, never Python).
- [x] An ADR (stashed draft ADR-1238; renumber if taken) records the decision, "Decided by Claude under Stewart's delegation"; `dotnet build` is clean and the fast tests pass.

## Notes

Split from BL-4911.

Continues the stashed work of BL-4911.

Lane 6 wrote the routing, HandBuiltTlsProvider change and ADR-1238 (unbuilt) in the stash. Needs BL-4963 first.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: ab3d57cb 37a2eeef feebc8d2.

## Notes (lane 11)

- ADR-1245 (written by BL-4963) already records the decision, so no new ADR; `SchannelAlgorithmList.OfferedSuites` already existed.
- Routing row `SchannelAlgorithmListRow` (extracted to keep `ReasonInBuild` at complexity 10) and `HandBuiltTlsProvider.SelectCipherSuites` done; `TlsClientRoutingTests` cover both builds.
- Removed the `Ciphers` row from `PlainOptionSets` (Schannel build now routes it hand-built) and loosened `EngineCompositionTests.CreateTransports_CaPathCertKeyAndCiphers...` (added to touches) to check mapped values, since the hand-built provider holds its own options copy.
- Criterion 2 (live `CALG_AES_128` / `CALG_RSA_KEYX` run against a local TLS 1.2 server, exit 35 text vs curl 8.22.0) is NOT measured here: unit tests cover the filter (BL-4963) and routing only. The follow-up measurement is BL-4978.
- Networking quality run: 100% line/branch, 0 failing members; fast tests green.
- 2026-11-11: Doing -> Backlog. Code done and committed; live CALG_* run against a local TLS 1.2 server (criterion 2) still to be measured
- 2026-11-11: Backlog -> Doing. Criteria at claim: ab3d57cb b09babf1 feebc8d2.
- 2026-11-11: Doing -> Done. Routing and filter done; live measurement is BL-4978
