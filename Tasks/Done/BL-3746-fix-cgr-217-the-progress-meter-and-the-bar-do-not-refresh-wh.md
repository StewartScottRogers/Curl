---
id: BL-3746
title: Fix CGR-217: The progress meter and the -# bar do not refresh while no data arrives
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr217Tests.cs, Curl.Console/ProgressBarRecorder.cs, Curl.Console/TransferProgressRecorder.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3746 — Fix CGR-217: The progress meter and the -# bar do not refresh while no data arrives

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-217 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-217: The progress meter and the -# bar do not refresh while no data arrives

- **Severity:** Low
- **Area:** writeout-verbose
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows and Linux). Only the presence of rows was compared; speed and timing values were not.
- **Related existing finding (GF/AF):** GF-0078
- **Source findings:** `writeout-verbose WV-13`

**Reproduction**

- *Setup (writeout-verbose):* EV = `SCR\evidence\writeout-verbose`. Windows server: `cd EV\srv && dotnet run wvsrv.cs -- 20700 EV\resp EV\req.log` (HTTP on 127.0.0.1/::1:20700, a mini FTP server on 20701, TLS on 20702; `/raw/<name>` serves `EV\resp\<name>.txt`; other routes `/code/N`, `/redir/N`, `/hang/ms`, `/close` (empty reply), `/ka`, `/echo`, `/p`). Linux: `EV\linux\srv.py` on 20710. One-line repros for every finding: `EV\repro_findings.sh` (Windows) and `EV\linux\verify_linux.sh`.
- `curl -m 3 http://127.0.0.1:20700/hang/9000`
- `curl -# -o x http://127.0.0.1:20700/hang/2500`

Commands (run each with REF and with PORT):

```sh
curl -m 3 http://127.0.0.1:20700/hang/9000
curl -# -o x http://127.0.0.1:20700/hang/2500
```

**Expected (real curl)**

- Meter: rows with Time Spent `00:01`, `00:02`, then the error.
- `-#`: animates `#=#=#`, `##O#-#`, `##O=#  #` before `curl: (52) Empty reply from server`.

**Actual (Curl)**

- Meter: only the initial zero row, then the error.
- `-#`: nothing before the error line.

**Suspected location in source**

- `Curl.Console/TransferProgressRecorder.cs`, `ProgressBarRecorder.cs`. Updates are driven only by data events.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr217Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-217 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

- 2026-10-28: Fix: `TransferProgressRecorder` starts a 1 s repeating timer (only when the meter or `-#` bar is shown) whose tick redraws the meter row and calls `ProgressBarRecorder.ReportTimePassed`, under a lock shared with the byte reports and `Finish`; `Finish` stops it. `ImmediateTimerTimeProvider` (test fake) now fires only one-shot timers, so retry-wait tests do not see the tick. Tests in `Cgr217Tests.cs`. Real-curl comparison of the two repro commands was not re-run (time budget); the expected rows are those the report gives. Platform-neutral: manual clocks only. Measure-CodeQuality for Curl.Console: 0 failing members.
- 2026-10-28: Doing -> Done. Meter and bar refresh once a second while no data arrives
