---
id: BL-5723
title: Fix AF-0954: Surviving mutant: a chain can be refused as relying on an unsent intermediate when no peer certificates were captured
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-5723 — Fix AF-0954: Surviving mutant: a chain can be refused as relying on an unsent intermediate when no peer certificates were captured

## Goal

The defect the audit office reported as AF-0954 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0954 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:343`

Location: `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:343`

Line 343: && peerCertificates.Length > 0 mutated to >= 0 survived. With an empty peerCertificates array the mutant goes on to Any(element => !WasSent(...)), finds every middle chain element unsent and Judge (line 305) refuses the handshake with exit 60 'unable to get local issuer certificate (20)'. The guard decides whether the OpenSSL-build refusal runs at all, and no test passes an empty peer list.

Reproduction: the audit office's mutation test changed `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs line 343` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs line 343`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by `ServerCertificateVerificationUnsentIntermediateTests.Judge_NoPeerCertificatesCapturedInTheOpenSslBuild_DoesNotRefuseAsAnUnsentIntermediate` (mutant `>= 0` at line 343 fails it by hand; reverted, all 4403 fast Networking tests pass). Test-only change, no production code touched.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 8f1162dd 9742157d.
- 2026-11-21: Doing -> Done. Mutant killed by new empty-peer-list test
