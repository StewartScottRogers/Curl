---
id: BL-448
title: Bring SslStreamTlsProvider.VerifyPeer back to cyclomatic complexity 10 or less
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-448 — Bring SslStreamTlsProvider.VerifyPeer back to cyclomatic complexity 10 or less

## Goal

`Measure-CodeQuality.ps1` reports no failing member in `Curl.Networking.UnitLibrary`, because `SslStreamTlsProvider.VerifyPeer` is at cyclomatic complexity 10 or less.

## Context

- Found while verifying BL-417 on 2026-09-27: `Measure-CodeQuality.ps1` lists `SslStreamTlsProvider.VerifyPeer(SslPolicyErrors, X509Chain, string, X509Certificate2Collection)` (`Curl.Networking.UnitLibrary\SslStreamTlsProvider.cs:294`) at complexity 12, failing the complexity gate, though coverage is 100%.
- The threshold in `CodeMetricsConfig.txt` is Stewart's; do not raise it. Extract a private method instead, as `.claude/rules/csharp-style.md` asks.
- Behaviour must not change: the existing `SslStreamTlsProviderTests` stay green unchanged.

## Acceptance criteria

- [x] `Measure-CodeQuality.ps1` reports 100% line and branch coverage and 0 failing members for `Curl.Networking.UnitLibrary`.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Extracted the Schannel build's failure answer from `VerifyPeer` into the private `SchannelPeerVerificationFailure`; `VerifyPeer` now picks the build with one conditional. Behaviour unchanged, tests untouched. Measured 2026-09-27: 287 members, 0 failing, worst CRAP 10, 100% line and branch.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. SslStreamTlsProvider.VerifyPeer is under the complexity limit; Curl.Networking.UnitLibrary has 0 failing members
