---
id: BL-2255
title: Fix AF-0218: HttpLineReader.ReadLineAsync's 'lineFeed >= 0' can become 'lineFeed > 0' with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-14
completed:
---
# BL-2255 — Fix AF-0218: HttpLineReader.ReadLineAsync's 'lineFeed >= 0' can become 'lineFeed > 0' with no test failing

## Goal

The defect the audit office reported as AF-0218 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0218 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0218-httplinereader-readlineasync-s-linefeed-0-can-beco.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpLineReader.cs:70`

Location: `Curl.Protocol.Http.UnitLibrary/HttpLineReader.cs:70`

Sampled run (seed 0) of Curl.Protocol.Http.UnitLibrary: mutant 'if (lineFeed >= 0)' -> 'if (lineFeed > 0)' in ReadLineAsync survived. lineFeed is an absolute buffer index, so it is 0 when a line starts at buffer[0] and is a bare LF. That happens for a response whose first byte is LF, or for an LF-only blank line left at the front after MakeRoom compacts a full buffer. Under the mutant that line is not returned: it is run through RejectHttp09 as a status line, or merged with the next line, so the head ends in the wrong place. No test puts a lone LF at buffer index 0.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpLineReader.cs:70:>= -Member ReadLineAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
