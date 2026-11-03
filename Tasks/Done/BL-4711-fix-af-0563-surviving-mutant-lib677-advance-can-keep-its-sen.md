---
id: BL-4711
title: Fix AF-0563: Surviving mutant: Lib677.Advance can keep its send position after a zero-length send without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests/Lib666To695Tests.cs]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4711 — Fix AF-0563: Surviving mutant: Lib677.Advance can keep its send position after a zero-length send without a test failing

## Goal

The defect the audit office reported as AF-0563 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0563 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0563-surviving-mutant-lib677-advance-can-keep-its-send.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib677.cs:188`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib677.cs:188`

Mutant 'position = sent || length > 0 ? position + length : 0;' -> 'length >= 0' survived. A zero-length curl_easy_send while the command is half sent no longer restarts the command from 0, which changes the bytes the port sends; no test drives a zero-length send.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib677.cs:188:> -Member Advance -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `Lib677Exchange_ASendOfNothingWithTheCommandHalfSent_SendsTheWholeCommandAgain`: 3 bytes go out, then a send of nothing drops the socket, then a second done message hands the socket back and the whole command goes again (upstream lib677's `pos = 0`). No production change.
- Lanes may not run the audit tool, so I applied the mutant (`length >= 0`) by hand with the Edit tool. The new test failed against it (the command was not restarted), and I reverted the mutant. A re-audit runs the real reproduction.
- I widened touches to the test file the fix needs. BL-4704 in Doing names the whole Curl.Conformance.LibTest.UnitTests project, but its work is Lib566 (Lib554To566Tests.cs), not this file. The two tasks share no file, so I did not requeue this one.
- The test uses only in-memory fakes, so its result does not depend on the platform. Build clean and fast tests green on Windows.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. A zero-length send with the command half sent is now tested; the mutant is killed
