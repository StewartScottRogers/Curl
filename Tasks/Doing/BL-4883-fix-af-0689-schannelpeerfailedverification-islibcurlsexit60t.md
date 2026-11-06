---
id: BL-4883
title: Fix AF-0689: SchannelPeerFailedVerification_..._IsLibcurlsExit60Text never asserts exit 60
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-4883 — Fix AF-0689: SchannelPeerFailedVerification_..._IsLibcurlsExit60Text never asserts exit 60

## Goal

The defect the audit office reported as AF-0689 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0689 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0689-schannelpeerfailedverification-islibcurlsexit60tex.md`.

Location: `Curl.Networking.UnitTests/TlsFailureMessagesTests.cs:407`

Location: `Curl.Networking.UnitTests/TlsFailureMessagesTests.cs:407`

The name states exit 60. The only assertion is Assert.AreEqual("SSL peer certificate or SSH remote key was not OK", message). Nothing checks 60 or CurlExitCode.PeerFailedVerification, so the name promises an exit code the test never ties to this message.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/TlsFailureMessagesTests.cs -Pattern 'IsLibcurlsExit60Text' -Context 0,16
```

- Expected: The test body asserts CurlExitCode.PeerFailedVerification (60).
- Actual: The body's only assertion is Assert.AreEqual("SSL peer certificate or SSH remote key was not OK", message);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
