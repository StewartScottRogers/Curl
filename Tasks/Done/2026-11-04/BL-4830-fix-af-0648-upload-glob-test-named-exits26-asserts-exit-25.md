---
id: BL-4830
title: Fix AF-0648: Upload glob test named Exits26 asserts exit 25
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4830 — Fix AF-0648: Upload glob test named Exits26 asserts exit 25

## Goal

The defect the audit office reported as AF-0648 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0648 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0648-upload-glob-test-named-exits26-asserts-exit-25.md`.

Location: `Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:425`

Location: `Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:425`

The name says Exits26. The body asserts CurlExitCode.UploadFailed (25) and stdout '<url>nosuchfile 25'.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs -Pattern 'FirstMatchCannotBeOpened_Exits26' -Context 0,6
```

- Expected: The name and the asserted exit code agree.
- Actual: Assert.AreEqual((int)CurlExitCode.UploadFailed, exitCode);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The test at Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:425 already asserts `CurlExitCode.ReadError` (26) and stdout `<url>nosuchfile 26`, so its name and its assertions agree. An earlier change fixed it after the audit snapshot; no code change was needed. The reproduction now prints `Assert.AreEqual((int)CurlExitCode.ReadError, exitCode);`.
- On 2026-11-04 `dotnet build` was clean and the fast tests passed (exit 0, 115 test projects, no failures).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. Upload glob Exits26 test already asserts exit 26 (ReadError); name and assertion agree
