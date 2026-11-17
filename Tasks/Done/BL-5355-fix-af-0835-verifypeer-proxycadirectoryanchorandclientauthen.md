---
id: BL-5355
title: Fix AF-0835: VerifyPeer_ProxyCaDirectoryAnchorAndClientAuthenticationLeaf_IsExit60UnsuitablePurpose never checks exit 60
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests/ServerCertificateVerificationTests.CaDirectoryPurpose.cs]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-5355 — Fix AF-0835: VerifyPeer_ProxyCaDirectoryAnchorAndClientAuthenticationLeaf_IsExit60UnsuitablePurpose never checks exit 60

## Goal

The defect the audit office reported as AF-0835 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0835 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitTests/ServerCertificateVerificationTests.CaDirectoryPurpose.cs:77`

Location: `Curl.Networking.UnitTests/ServerCertificateVerificationTests.CaDirectoryPurpose.cs:77`

The test's only assertion is line 77: Assert.AreEqual(UnsuitablePurpose, failure?.Message);. Verify returns (CurlExitCode ExitCode, string Message)?, and failure?.ExitCode is never asserted. The sibling tests at lines 46 and 64 do assert Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode). A wrong exit code with the right text would pass. Found by Find-WeakTests (name-lies); no quoted assertion checks 60.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/ServerCertificateVerificationTests.CaDirectoryPurpose.cs -Pattern 'PeerFailedVerification, failure\?\.ExitCode'
```

- Expected: Four matches: each IsExit60 test asserts the exit code.
- Actual: Two matches (lines 46 and 64); the tests at lines 69 and 94 assert only the message.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Added the missing exit-code assertion to the two tests that asserted only the message (proxy CA directory and CA file). Targeted tests pass (11); test-only change.


## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-17: Doing -> Done. Added exit-code assertions to the two message-only tests; targeted tests pass
