---
id: BL-1361
title: Fix AF-0026: Five ECH tests in Curl.Networking.UnitTests fail on the unmutated tree, which blocks mutation testing of Curl.Networking.UnitLibrary
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-03
completed:
---
# BL-1361 — Fix AF-0026: Five ECH tests in Curl.Networking.UnitTests fail on the unmutated tree, which blocks mutation testing of Curl.Networking.UnitLibrary

## Goal

The defect the audit office reported as AF-0026 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0026 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0026-five-ech-tests-in-curl-networking-unittests-fail-o.md`.

Location: `Curl.Networking.UnitTests`

Location: `Curl.Networking.UnitTests`

dotnet test Curl.Networking.UnitTests -c Release gives Failed: 5, Passed: 2929, Skipped: 31. Failing tests include AuthenticateAsClientAsync_WithEchHardAndNoUsableList_FailsWithExit35BeforeSendingAByte, AuthenticateAsClientAsync_WithEchTrueAndNoUsableList_SendsAPlainHello and AuthenticateAsClientAsync_WithEch_WritesCurlsEchLinesBeforeTheHello. Invoke-MutationTest.ps1 stops with baselineError 'the unmutated tests failed or timed out (exit 1)' and score null, so none of the library's survivors can be killed or confirmed. The test file path in the key is a best guess; the failing tests are in Curl.Networking.UnitTests.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet test Curl.Networking.UnitTests -c Release -nologo
```

- Expected: Passed! with Failed: 0
- Actual: Failed! - Failed: 5, Passed: 2929, Skipped: 31, Total: 2965

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
