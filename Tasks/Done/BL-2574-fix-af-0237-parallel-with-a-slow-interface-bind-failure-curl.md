---
id: BL-2574
title: Fix AF-0237: --parallel with a slow --interface bind failure: Curl prints meter rows before the curl: (45) line and runs the error on after them; curl prints the error first
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2574 — Fix AF-0237: --parallel with a slow --interface bind failure: Curl prints meter rows before the curl: (45) line and runs the error on after them; curl prints the error first

## Goal

The defect the audit office reported as AF-0237 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0237 (Low, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0237-parallel-with-a-slow-interface-bind-failure-curl-p.md`.

Location: `Curl.Console/ParallelProgressMeter.cs`

Location: `Curl.Console/ParallelProgressMeter.cs`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 374343970, case 189 (--parallel --interface - --crlf URL), reduced to --parallel --interface - URL; reproduced twice. curl stderr starts 'curl: (45) Failed to connect to 127.0.0.1:PORT after N ms: Failed binding local connection end' as its own first line, then the meter header and rows. Curl stderr starts with the meter header, then '\r--  --      0     0     1     1 ...' rows drawn during the ~2.7 s bind attempt, with 'curl: (45) ...' run on after the last row on the same line. Both exit 45, and without --parallel both print just the error line. A probable cause, not confirmed: curl's interface lookup blocks its parallel loop, so it draws nothing until the transfer fails, while Curl's asynchronous connect lets ParallelProgressMeter draw on its one-second tick (ADR-0155). Phase 2: ADR-0155 describes drawing on the loop's events and its one-second poll; it does not cover a connect that blocks curl's loop.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -v q; foreach ($c in @('C:\Program Files\Git\ucrt64\bin\curl.exe', (Resolve-Path Curl.Console\bin\Release\net10.0\curl.exe).Path)) { $o = Join-Path $env:TEMP ('t189-' + [guid]::NewGuid()); .\Record-CurlExchange.ps1 -Port 63411 -Curl $c -OutDirectory $o -CurlArgs @('--parallel','--interface','-','http://127.0.0.1:63411/') *> $null; '--'; (Get-Content (Join-Path $o 'stderr.txt'))[0] }
```

- Expected: Each binary's first stderr line is 'curl: (45) Failed to connect to 127.0.0.1:63411 after N ms: Failed binding local connection end'
- Actual: curl's first line is the curl: (45) line; Curl's first line is the meter header 'DL% UL%  Dled  Uled  Xfers  Live Total     Current  Left    Speed', and its curl: (45) text follows meter rows on line 2

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `Curl.Console.UnitTests` to `touches` for the tests; no task in Doing on `origin/work/dark-factory` named it.
- Decision (ADR-0661): a `-Z` transfer given `--interface` holds the combined meter's draws
  (`ParallelTransferProgress.HoldsTheLoop`) until it reports itself past connect or ends, as curl's
  `bindlocal` blocks its loop while it resolves the name. `TransferProgressRecorder` now passes
  `ReportTransferStarted` on to the transfer's share. The hold lasts to the end of connect, not just the
  bind, because the connector reports nothing between them; recorded as the one known difference.
- Measured 2026-10-17 with the finding's reproduction: curl 8.22.0 (Git ucrt64) and the Release build both
  start standard error with `curl: (45) Failed to connect to 127.0.0.1:63411 after N ms: Failed binding local
  connection end`, then the header and two status lines.
- `Measure-CodeQuality.ps1 -Library Curl.Console` found `ParallelProgressMeter.Draw` at complexity 12; the due
  check moved into `IsDueAndNotHeld`, which brings it back to its old shape. No other member failed.
- Tests: `ParallelProgressMeterTests.DrawIfDue_TransferBindingItsInterface_DrawsNothingUntilItEnds`,
  `ReportTransferStarted_TransferThatBoundItsInterface_DrawsTheHeldLine`, and
  `CurlCommandRunnerParallelProgressMeterTests.RunAsync_ParallelWithInterfaceBindThatFails_WritesTheErrorLineBeforeTheMeter`.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-17: Doing -> Done. AF-0237 reproduction now prints curl: (45) before the -Z meter, as curl does; build and fast tests green
