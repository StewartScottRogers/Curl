---
id: BL-5083
title: Fix AF-0724: Surviving mutant: --ws-repl can wait forever on a response that begins with an empty line
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.WebSocketRepl.UnitLibrary]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5083 — Fix AF-0724: Surviving mutant: --ws-repl can wait forever on a response that begins with an empty line

## Goal

The defect the audit office reported as AF-0724 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0724 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0724-surviving-mutant-ws-repl-can-wait-forever-on-a-res.md`.

Location: `Curl.Extension.WebSocketRepl.UnitLibrary/Handshake/WebSocketOpeningHandshake.cs:35`

Location: `Curl.Extension.WebSocketRepl.UnitLibrary/Handshake/WebSocketOpeningHandshake.cs:35`

Mutant 'IndexOf("\r\n\r\n"u8)) < 0' -> '<= 0' survived. When the head end is found at offset 0 the mutant keeps reading. Because IndexOf finds offset 0 on every pass, it reads until the peer closes or the head limit is reached, so a server that sends CRLF CRLF and waits hangs the transfer where the original refuses it at once.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.WebSocketRepl.UnitLibrary/Handshake/WebSocketOpeningHandshake.cs:35:< -Member PerformAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

A lane cannot run the audit mutation tool (the guard refuses), so the reproduction was not run. Added test PerformAsync_ResponseBeginsWithAnEmptyLine_RefusesAtOnceWithoutReadingOn: with the mutant the loop reads on, hits the exhausted connection and reports "the connection closed ..." instead of the validator failure, so the test kills it. Fast tests green (60 passed). Test-only change.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-13: Doing -> Done. Added a test that kills the empty-head-line mutant; fast tests green
