---
id: BL-5791
title: Fix AF-0989: Surviving mutant: the --capath reason chain can be rebuilt without requiring the server-auth purpose (true -> false)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs, Curl.Networking.UnitTests/RebuiltChainVerifyReasonTests.cs]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5791 — Fix AF-0989: Surviving mutant: the --capath reason chain can be rebuilt without requiring the server-auth purpose (true -> false)

## Goal

The defect the audit office reported as AF-0989 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0989 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs:207`

Location: `Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs:207`

Seeded mutation run (with the 3 failing ECH tests left out): 'RebuildAgainstAnchors(chain, anchors, requireServerAuthentication: true, out _)' -> 'requireServerAuthentication: false' survived. ReasonChainFromAnchors picks the chain whose status gives the OpenSSL build's verify reason. Without the server-auth requirement, a leaf with the wrong extended key usage under a --capath root reports a reason from a chain that ignores the purpose, so the error text changes. No test covers a wrong-purpose leaf on this path.

Reproduction: the audit office's mutation test changed `Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs line 207` as the evidence above describes (the mutant operator is `true`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs line 207`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Killed by new test `RebuiltChainVerifyReasonTests.Judge_CaDirectoryAnchorAndExpiredClientAuthenticationLeaf_ReportsVerifyResultOfTheChainRequiringServerPurpose` (expired leaf with client-auth EKU: 26 with the purpose required, 10 with the mutant). Verified by hand: mutant fails it, reverted passes all 4408 fast tests of Curl.Networking.UnitTests. Test file added to touches (tests only, no production change).

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 4efe49ba 9742157d.
- 2026-11-22: Doing -> Done. Test pins the --capath reason chain's server-auth purpose requirement; mutant now fails it
