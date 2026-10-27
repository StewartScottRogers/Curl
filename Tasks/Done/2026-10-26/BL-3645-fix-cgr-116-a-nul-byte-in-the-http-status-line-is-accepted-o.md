---
id: BL-3645
title: Fix CGR-116: A NUL byte in the HTTP status line is accepted (output written, exit 0); curl fails with exit 8 Nul byte in header
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpResponseHeadBuilder.cs, Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs, Curl.Protocol.Http.UnitTests/Cgr116Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3645 — Fix CGR-116: A NUL byte in the HTTP status line is accepted (output written, exit 0); curl fails with exit 8 Nul byte in header

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-116 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-116: A NUL byte in the HTTP status line is accepted (output written, exit 0); curl fails with exit 8 `Nul byte in header`

- **Severity:** Medium
- **Area:** upstream-cases, http-responses
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: [upstream-cases UC-1] Confirmed (on PORT-WIN and PORT-LINUX, in both variants).
- **Related existing finding (GF/AF):** GF-0012
- **Source findings:** `upstream-cases UC-1`, `http-responses HR-4`

**Reproduction**

- *Setup (upstream-cases):* EV = `SCR\evidence\upstream-cases`. Cross-platform runs: `EV\verify\diff.sh`, driven in WSL by `MSYS_NO_PATHCONV=1 wsl -d Ubuntu -- bash "/mnt/c/Users/Stewart Rogers/AppData/Local/Temp/claude/Z--repos-Curl-ThirdPartyReview/3796612b-55d9-44ec-9722-80e35c3282a1/scratchpad/evidence/upstream-cases/verify/sync.sh" vN.sh` (N = 1..4), against the canned recording server `EV\verify\srv.py` (configs in `EV\verify\conf\`). Windows-native runs: `EV\verify\vwin.ps1` (wraps `harness\Diff-Curl.ps1` and `Record-CurlExchange.ps1`).
- *Setup (http-responses):* EV = `SCR\evidence\http-responses`. Canned-response server: `dotnet run EV\server\server.cs -- 20400 EV\resp EV\logs`; `GET /NAME` returns the bytes of `EV\resp\NAME.bin` (if `EV\resp\NAME.opts` contains `keep`, the connection stays open). Differential: `EV\diff.ps1 -Name NAME -CArgs ... [-Opts keep]`; verifier `EV\verify\vdiff.ps1` (port 20420) driven by `EV\verify\vcases.ps1`. Linux: `EV\linux\server.py` serves the same fixtures (verifier: port 20460 via `EV\verify\linux\vrun.sh`; port 20450 in the original runs).
- **[upstream-cases UC-1]** NUL byte in the HTTP status line is accepted: PORT prints the response and exits 0, where curl fails with exit 8
  - Server conf `verify\conf\nulstatus.json` replies `HTTP/1.1 200 OK\x00\r\nContent-Length: 6\r\n\r\n-foo-\n`.
  - Run `curl -s -i http://127.0.0.1:21350/x` and `curl -s -S http://127.0.0.1:21350/x` (script `verify\v1.sh`, `v3.sh`).
  - test262 variant: `verify\conf\nul262.json` uses LF line ends and puts a NUL in the status line, in Date and in ETag. Run `curl -s -i http://127.0.0.1:21350/262`.
- **[http-responses HR-4]** NUL byte in the status line is accepted (curl fails with exit 8 "Nul byte in header")
  - `st_reason_nul.bin` = `HTTP/1.1 200 O\x00K\r\nContent-Length: 2\r\n\r\nok`. Command: `curl -s -S http://127.0.0.1:20400/st_reason_nul`
  - `st_code_nul` (`HTTP/1.1 200\x00`), run with `-i`.
  - `st_reason_nul_end` (`HTTP/1.1 404 Not\x00`), run with `-f`.

Commands (run each with REF and with PORT):

```sh
# [upstream-cases UC-1]
curl -s -i http://127.0.0.1:21350/x
curl -s -S http://127.0.0.1:21350/x
curl -s -i http://127.0.0.1:21350/262
# [http-responses HR-4]
curl -s -S http://127.0.0.1:20400/st_reason_nul
```

**Expected (real curl)**

- **[upstream-cases UC-1]** Exit 8, `curl: (8) Nul byte in header`, and stdout is empty. REF-WIN 8.22.0 and REF-LINUX 8.18.0 agree.
- **[http-responses HR-4]** exit 8 `curl: (8) Nul byte in header`, nothing on stdout. Also exit 8 in the `-f` case.

**Actual (Curl)**

- **[upstream-cases UC-1]** 
  - Exit 0. stdout is `HTTP/1.1 200 OK\0\r\nContent-Length: 6\r\n\r\n-foo-\n` (45 bytes), and nothing is printed on stderr.
  - In the test262 variant PORT also exits 8, but has already written `HTTP/1.1 200 OK\0\n` (17 bytes) to stdout.
- **[http-responses HR-4]** exit 0 with body `ok`. With `-i` it writes the status line, NUL included. With `-f` on the 404 variant it exits 22. Same on Linux.

**Suspected location in source**

- **[upstream-cases UC-1]** `Curl.Protocol.Http.UnitLibrary\HttpResponseHeadBuilder.cs:54-58`. `StartHead()` passes the status line to `Append()` without the NUL check that `AddLine()` makes at lines 77-80.
- **[http-responses HR-4]** `HttpResponseHeadReader.cs` `ReadStatusLineAsync` (~511). The NUL check exists only in `HttpResponseHeadBuilder.AddLine`, which handles header lines, not the status line.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr116Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Fix: `HttpResponseHeadBuilder.StartHead` now refuses a status line holding a NUL byte with exit 8 `Nul byte in header` (the check `AddLine` already made, shared as `RefuseNulByte`), before the line is written or reported; as in curl's `http_rw_hd`, the status line is parsed first, so a non-HTTP first line still takes the HTTP/0.9 path. No ADR: the behaviour is curl's, measured, with no design choice to record.
- Measured 2026-10-26 with `Record-CurlExchange.ps1` (port 21645), real curl 8.22.0 (mingw, Schannel) and then Curl's `curl.exe` against the same canned responses; exit code, stdout.bin, stderr.txt and request.bin compared by hash, all equal:
  - `-s -i` on `HTTP/1.1 200 OK\0\r\nContent-Length: 6\r\n\r\n-foo-\n`: REF exit 8, stdout empty, stderr empty. PORT the same (was exit 0, 45 bytes on stdout).
  - `-s -S` on the same: REF exit 8, `curl: (8) Nul byte in header`, stdout empty. PORT the same.
  - `-s -i` on the test262 shape (LF line ends, NUL in status line, Date and ETag): REF exit 8, stdout empty. PORT the same (was exit 8 with 17 bytes on stdout).
  - `-s -S` on `HTTP/1.1 200 O\0K` (st_reason_nul): REF exit 8, `curl: (8) Nul byte in header`. PORT the same (was exit 0, `ok`).
  - `-s -S -i` on `HTTP/1.1 200\0` (st_code_nul): REF exit 8, `curl: (8) Nul byte in header`, stdout empty. PORT the same.
  - `-s -S -f` on `HTTP/1.1 404 Not\0` (st_reason_nul_end): REF exit 8, `curl: (8) Nul byte in header`. PORT the same (was exit 22).
- No variant matched before the fix; the existing header-line NUL test (`ExecuteAsync_NulByteInAHeaderLine_FailsWithWeirdServerReplyBeforeReportingIt`) still passes.
- Focused test `Cgr116Tests.cs` (`ExecuteAsync_NulByteInTheStatusLine_FailsWithWeirdServerReplyBeforeReportingIt`, five rows): all five failed before the fix and pass after.
- `dotnet build` clean; fast tests 43940 passed of 44235 (the rest skipped), 0 failed. `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 0 failing members.
- Linux: the check is byte-level with no platform-dependent text; REF-LINUX 8.18.0 gives the same exit 8 per the report; kept platform-neutral, CI runs it on Linux and macOS.
## Log

- 2026-10-26: Created from CGR-116 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. A NUL byte in the HTTP status line fails with exit 8 Nul byte in header and nothing written, as curl 8.22.0 does
