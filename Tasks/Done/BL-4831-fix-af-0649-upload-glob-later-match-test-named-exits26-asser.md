---
id: BL-4831
title: Fix AF-0649: Upload glob (later match) test named Exits26 asserts exit 25
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4831 — Fix AF-0649: Upload glob (later match) test named Exits26 asserts exit 25

## Goal

The defect the audit office reported as AF-0649 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0649 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0649-upload-glob-later-match-test-named-exits26-asserts.md`.

Location: `Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:449`

Location: `Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:449`

The name says Exits26. The body asserts CurlExitCode.UploadFailed (25) and stdout 'http://h/g/nosuchfile 25'.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs -Pattern 'LaterMatchCannotBeOpenedAfterASuccess_Exits26' -Context 0,6
```

- Expected: The name and the asserted exit code agree.
- Actual: Assert.AreEqual((int)CurlExitCode.UploadFailed, exitCode);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The test at Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:449 already asserts `CurlExitCode.ReadError` (26) and stdout `http://h/g/nosuchfile 26`, so its name and its assertions agree. An earlier change fixed it after the audit snapshot, so no code change was needed. The reproduction now prints `Assert.AreEqual((int)CurlExitCode.ReadError, exitCode);`.
- On 2026-11-04 `dotnet build` was clean and the fast tests passed (dotnet test exit 0).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. Upload glob later-match Exits26 test already asserts exit 26 (ReadError); name and assertion agree
