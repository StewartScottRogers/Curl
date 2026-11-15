---
id: BL-5198
title: Fix AF-0749: ...KeepsTheIpNotAmongNamesMessage never asserts the message it names, only that it is not another one
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-11-14
completed: 2026-11-14
---
# BL-5198 — Fix AF-0749: ...KeepsTheIpNotAmongNamesMessage never asserts the message it names, only that it is not another one

## Goal

The defect the audit office reported as AF-0749 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0749 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitTests/ServerCertificateVerificationTests.VerifyChain.cs:301`

Location: `Curl.Networking.UnitTests/ServerCertificateVerificationTests.VerifyChain.cs:301`

The body asserts Assert.IsFalse(verdict.IsAccepted); and Assert.AreNotEqual("schannel: CertFindExtension() returned no extension.", verdict.ErrorMessage);. The name promises the IP-not-among-names message is kept, but any other wrong message (empty text, a different curl error) passes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/ServerCertificateVerificationTests.VerifyChain.cs -Pattern 'KeepsTheIpNotAmongNamesMessage' -Context 0,10
```

- Expected: Assert.AreEqual of the exact IP-not-among-names message.
- Actual: Only Assert.AreNotEqual against a different message.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Test now asserts the exact message "SSL peer certificate or SSH remote key was not OK" (TlsFailureMessages.SchannelIpAddressNotAmongAlternativeNames). CI on Linux and macOS: test is platform-neutral (matchesSchannelBuild forced), fast tests green here.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-14: Doing -> Done. IP-not-among-names test asserts the exact message
