---
id: BL-1863
title: Fix AF-0113: SendBodyAsync's no-body 'return false' can become 'return true' with no test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1863 — Fix AF-0113: SendBodyAsync's no-body 'return false' can become 'return true' with no test failing

## Goal

The defect the audit office reported as AF-0113 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0113 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0113-sendbodyasync-s-no-body-return-false-can-become-re.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:2433`

Location: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:2433`

Mutant 'return false;' -> 'return true;' at line 2433 survived (sampled run, seed 0). The return value is bodyLeftUnsent (line 1129): true means a final status arrived during the 100-continue wait and the body was left unsent. For a request with no body, the mutant makes ReportRequestSent skip nothing visible but makes RetryOfAsync/RetriesWithoutExpect (line 2148) treat a keep-alive 417 as 'body stopped': it reports 'Got 417 while waiting for a 100' and retries with plan.Framing.Body! being null. A bodyless request answered by 417 would then be resent or fail instead of ending with the 417, changing request bytes and exit code. No test sends a bodyless request that gets a 417.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:2433:false -Member SendBodyAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fix: new test `ExecuteAsync_417ToARequestWithNoBody_ReturnsItWithoutResending` in
  `Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.ExpectationFailed.cs`: a bodyless GET
  answered by a keep-alive 417 ends with the 417, exit 0, one request written, the 417 head in
  `-D`, nothing on stdout. No production change; the code was right, only untested.
- Added `Curl.Protocol.Http.UnitTests` to `touches`: the fix is a test, and no task in Doing on
  `origin/work/dark-factory` (BL-1862 Curl.Tls.UnitLibrary, BL-1866 Curl.Networking.UnitLibrary)
  names it.
- The audit guard refuses lanes `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was applied
  by hand instead: with line 2433 `return false;` changed to `return true;` the new test fails at
  `Assert.AreEqual(getHead, connection.Written)` (the mutant resends); with the original line it
  passes. The quality auditor's re-audit runs the tool itself.
- Not re-measured against real curl: curl sends no body and so never takes its 417 resend path
  (it needs `expect100` or a cut-short upload); the request bytes are the plain GET head the
  other tests in this project already pin.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. Bodyless request answered by a keep-alive 417 is now tested; the AF-0113 mutant is killed
