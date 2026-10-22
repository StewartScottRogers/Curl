---
id: BL-2569
title: Fix AF-0232: ReportUploadRewind's 3xx range can drop 300 with no test failing, losing curl's 'Need to rewind upload' -v line
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2569 — Fix AF-0232: ReportUploadRewind's 3xx range can drop 300 with no test failing, losing curl's 'Need to rewind upload' -v line

## Goal

The defect the audit office reported as AF-0232 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0232 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0232-reportuploadrewind-s-3xx-range-can-drop-300-with-n.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1584`

Location: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1584`

Mutant '&& statusLine.StatusCode is >= 300 and < 400' -> '> 300 and < 400' survived (seed 0, sample of 40). With -L and a non-empty body, a 300 Multiple Choices reply would no longer write '* Need to rewind upload for next request' (nor the close line after it) to the verbose output, which is stderr output bytes. The upload-rewind tests use 301/302/303/307/308 only.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1584:>= -Member ReportUploadRewind -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Measured real curl 8.21.0 (Schannel) first, 2026-10-17: `Record-CurlExchange.ps1 -Port 47811 -Connections 1`, answer `300 Multiple Choices` with `Location: /b`, `Content-Length: 0`, `Connection: close`, curl args `-v -d ab -L --max-redirs 0 http://127.0.0.1:47811/a`. stderr has `* Need to rewind upload for next request` right after `< HTTP/1.1 300 Multiple Choices`, so 300 belongs in the range and the production code stays as it is.
- Fix: a `300 Multiple Choices` row on `ExecuteAsync_FollowedRedirectAfterABody_ReportsTheRewindRightAfterTheStatusLine` in `Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.UploadRewind.cs`, and its class summary names 300 as the low end of the range. Test-only change, so no Measure-CodeQuality run was needed.
- Touches: added `Curl.Protocol.Http.UnitTests`, the only place the killing test can go; no other task in Doing on origin/work/dark-factory (BL-2568: Curl.Tls.UnitLibrary, BL-2576: Curl.Console) names it.
- Reproduction: the audit guard refuses lanes running `Audit/Tools/Invoke-MutationTest.ps1`, so the same mutant was applied by hand (line 1584 `is >= 300` -> `is > 300`): the new 300 row failed (1 failed, 17 passed in the rewind tests), so the mutant is killed; the line was then restored. The quality auditor's re-audit runs the script itself.
- Fast run: everything passed except `ListenAsync_WhenTheFirstPortIsTaken_ListensOnTheNextFreeOne` in Curl.Networking.UnitTests (untouched here), which passed when run alone; filed as BL-2583.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-17: Doing -> Done. A 300 Multiple Choices row now pins the rewind line, killing AF-0232's mutant
