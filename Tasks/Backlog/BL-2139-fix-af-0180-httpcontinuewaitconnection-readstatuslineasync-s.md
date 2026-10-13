---
id: BL-2139
title: Fix AF-0180: HttpContinueWaitConnection.ReadStatusLineAsync's 'IndexOf(...) < 0' can become '<= 0' with no test failing: a reply starting with a bare LF no longer ends the 100-continue wait at once
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-13
completed:
---
# BL-2139 — Fix AF-0180: HttpContinueWaitConnection.ReadStatusLineAsync's 'IndexOf(...) < 0' can become '<= 0' with no test failing: a reply starting with a bare LF no longer ends the 100-continue wait at once

## Goal

The defect the audit office reported as AF-0180 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0180 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0180-httpcontinuewaitconnection-readstatuslineasync-s-i.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs:170`

Location: `Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs:170`

Sampled mutation (seed 0) of Curl.Protocol.Http.UnitLibrary: 'while ((lineFeed = Array.IndexOf(received, (byte)'\n', 0, receivedLength)) < 0 && receivedLength < received.Length)' mutated to '<= 0' survived. With the original, a first line feed at offset 0 ends the read: StatusCodeOf gives null and the wait learns at once that no 100 Continue came. With the mutant, IndexOf keeps returning 0, so the loop keeps reading until the connection ends or the 16 KB buffer fills. The expect-100 wait therefore runs to its timeout and sends the body to a server that has already answered. That is a decision about which request bytes are sent, and no test feeds a reply whose first byte is a line feed.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs:170:< -Member ReadStatusLineAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-13: Created.
