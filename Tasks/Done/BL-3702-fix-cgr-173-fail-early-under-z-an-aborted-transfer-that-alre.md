---
id: BL-3702
title: Fix CGR-173: --fail-early under -Z: an aborted transfer that already had its response loses %{http_code} and %{filename_effective}, and --remove-on-er...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr173Tests.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console/RunningTransferState.cs, Curl.Console/ResponseCodeRecordingTransferEvents.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3702 — Fix CGR-173: --fail-early under -Z: an aborted transfer that already had its response loses %{http_code} and %{filename_effective}, and --remove-on-er...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-173 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-173: --fail-early under -Z: an aborted transfer that already had its response loses %{http_code} and %{filename_effective}, and --remove-on-error leaves its partial file

- **Severity:** Medium
- **Area:** parallel-multi-transfer
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows x2, Linux)
- **Related existing finding (GF/AF):** none
- **Source findings:** `parallel-multi-transfer PMT-5`

**Reproduction**

- *Setup (parallel-multi-transfer):* EV = `SCR\evidence\parallel-multi-transfer`. Server: `dotnet run EV\srv\server.cs -- 22000,22001 EV\srv\log` (listens on 127.0.0.1:22000 and :22001; query `d=` delay in ms, `s=` status, `n=` body size, `p=`/`pd=` send the body in parts with a delay, `close=1` adds Connection: close; other paths return `body /<path>`); port 22099 is closed. Linux: `EV/linux/srv.py 22000,22001 /tmp/pmt_srv` (started by `EV/linux/lt.sh`). Harness `EV\dt.ps1`; verifier `EV\v1.ps1` and `EV/linux/vl.sh`.
- `curl -Z --parallel-immediate -s --remove-on-error --fail-early -f -w "%{urlnum} %{exitcode} %{http_code} %{filename_effective}\n" -o a.txt "http://127.0.0.1:22000/a?n=50000&p=5&pd=300" -o b.txt "http://127.0.0.1:22000/b?d=200&s=500"`. Scripts: `v1.ps1` (v_rmerr_fe, v_rmerr_fe_r), `linux/vl.sh` (vl_rmerr_fe).

Commands (run each with REF and with PORT):

```sh
curl -Z --parallel-immediate -s --remove-on-error --fail-early -f -w "%{urlnum} %{exitcode} %{http_code} %{filename_effective}\n" -o a.txt "http://127.0.0.1:22000/a?n=50000&p=5&pd=300" -o b.txt "http://127.0.0.1:22000/b?d=200&s=500"
```

**Expected (real curl)**

- stdout is `1 22 500 b.txt\n0 42 200 a.txt\n`. a.txt is removed and no files are left. Exit 22.

**Actual (Curl)**

- stdout is `1 22 500 b.txt\n0 42 000 \n`, so http_code is 000 and filename_effective is empty. a.txt (10000 bytes, partial) is left on disk. Exit 22.

**Suspected location in source**

- `Curl.Console/CurlCommandRunner.cs:1962-1967`. When the run is aborted, `EndParallelTransferAsync` reports `ParallelRun.AbortedResult` instead of the transfer's own result, so `RemoveOutputFileOfFailedTransferAsync` (:2186) never sees the opened file.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr173Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-28, lane 6. Repro rebuilt with a C# file-based loopback server on 127.0.0.1:22000 (outside the repo): `/a` answers `200` with `Content-Length: 50000` and sends 5 parts of 10000 bytes 300 ms apart, `/b` waits 200 ms and answers `500` with an empty body. Command: `curl -Z --parallel-immediate -s --remove-on-error --fail-early -f -w "%{urlnum} %{exitcode} %{http_code} %{filename_effective}\n" -o a.txt "http://127.0.0.1:22000/a?n=50000&p=5&pd=300" -o b.txt "http://127.0.0.1:22000/b?d=200&s=500"`.
  - REF (curl 8.22.0 Schannel, Windows): exit 22, stdout `1 22 500 b.txt\r\n0 42 200 a.txt\r\n`, stderr empty, no files left.
  - PORT before the fix: exit 22, stdout `1 22 500 b.txt\r\n0 42 000 \r\n`, stderr empty, a.txt (10000 bytes) left.
  - PORT after the fix: exit 22, stdout `1 22 500 b.txt\r\n0 42 200 a.txt\r\n`, stderr empty, no files left. Both send the same two `GET` requests (`Host`, `User-Agent: curl/8.22.0`, `Accept: */*`).
- Cause: the aborted transfer's handler throws `OperationCanceledException`, so `TransferToOutputFileAsync` never recorded `OutputFileName`/`OpenedOutputFile` (no `%{filename_effective}`, nothing for `--remove-on-error` to delete) and the result carried no report (`%{http_code}` 000). Fix: those two are set in a `finally`; under `-w` a new `ResponseCodeRecordingTransferEvents` records the last status line's code on `RunningTransferState.ReceivedResponseCode`, and `AbortedResultOf` gives `ParallelRun.AbortedResult` a report with that code.
- Decision (default taken): recorded on the Console side rather than widening the engine's `AbortedHeadRecordingTransferEvents` (timeouts only), because Curl.Engine is outside this task's touches and busy with other lanes; only `%{http_code}` is carried, the variable the issue names. `touches` gained `Curl.Console/RunningTransferState.cs` and `Curl.Console/ResponseCodeRecordingTransferEvents.cs` (no Doing task on origin/work/dark-factory names a Curl.Console file).
- `Cgr173Tests.RunAsync_FailEarlyAbortsATransferThatHadItsResponse_PrintsItsCodeAndFileAndRemovesTheFile` failed with the runner change reverted and passes with it. The only variant in the issue is the one command above. Platform: the tests use relative names in an in-memory file system and LF output (`runsOnWindows: false`), so they hold on Linux and macOS. `dotnet build` 0 warnings; fast tests green (Curl.Console.UnitTests 2998 passed, 31 skipped); `Measure-CodeQuality.ps1 -Library Curl.Console`: 0 failing members.

## Log

- 2026-10-26: Created from CGR-173 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Done. -Z --fail-early: an aborted transfer that had its response prints its %{http_code} and %{filename_effective}, and --remove-on-error deletes its partial file, as curl 8.22.0 does
