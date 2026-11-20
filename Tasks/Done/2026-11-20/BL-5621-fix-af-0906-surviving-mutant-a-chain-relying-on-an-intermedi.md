---
id: BL-5621
title: Fix AF-0906: Surviving mutant: a chain relying on an intermediate the server did not send can be observed as verified
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5621 — Fix AF-0906: Surviving mutant: a chain relying on an intermediate the server did not send can be observed as verified

## Goal

The defect the audit office reported as AF-0906 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0906 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:300`

Location: `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:300`

Invoke-MutationTest -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 912464: 'new PeerVerification(false, OpenSslVerifyResult.UnableToGetIssuerCertificateLocally, peerCertificates)' mutated to true survived. Judge returns this observation with the PeerFailedVerification refusal when RelyingOnAnIntermediateTheServerDidNotSend. Its Verified flag becomes the handshake event's CertificateVerified (SslStreamTlsProvider.cs:634/696), which NetworkDiagnosticLog.cs:294 writes as 'verified' or 'not verified'. With the mutant, a refused certificate is logged as verified, and no test notices. The refusal and exit 60 are unchanged, so this is a recorded decision, not exit-code output.

Reproduction: the audit office's mutation test changed `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs line 300` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs line 300`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by ServerCertificateVerificationUnsentIntermediateTests.Judge_IntermediateTheServerDidNotSendInTheOpenSslBuild_IsExit60WithError20, which now asserts observed.Verified is false (and the verify result is error 20). Test-only change; build and Networking fast tests green.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 61c80484 9742157d.
- 2026-11-20: Doing -> Done. Mutant killed by strengthened test
