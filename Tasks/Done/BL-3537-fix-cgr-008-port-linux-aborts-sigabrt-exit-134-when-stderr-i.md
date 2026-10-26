---
id: BL-3537
title: Fix CGR-008: PORT-LINUX aborts (SIGABRT, exit 134) when stderr is a full device (ENOSPC)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr008Tests.cs, Curl.Console/UnhandledExceptionGuard.cs, Curl.Console/StandardErrorFailureIgnoringStream.cs, Curl.Console/Program.cs, Curl.Console/CLAUDE.md]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3537 — Fix CGR-008: PORT-LINUX aborts (SIGABRT, exit 134) when stderr is a full device (ENOSPC)

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-008 (severity Critical): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-008: PORT-LINUX aborts (SIGABRT, exit 134) when stderr is a full device (ENOSPC)

- **Severity:** Critical (crash)
- **Area:** network-robustness
- **Platform(s):** Linux
- **Confidence:** Confirmed. Detail: Confirmed. This is a process crash, so the REF-LINUX 8.18 version does not explain it.
- **Related existing finding (GF/AF):** none
- **Source findings:** `network-robustness NR-1`

**Reproduction**

- *Setup (network-robustness):* EV = `SCR\evidence\network-robustness`. Windows server: `EV\srvbin\server.exe 21100 21101` (source `EV\srv\server.cs`); repro script `EV\repro_findings.sh` (uses the `d.sh` diff harness). Linux: `python3 EV/linux/srv.py 21150 21151`; repro `EV/linux/verify_lin.sh`. `H=http://127.0.0.1:21100` (21150 on Linux); `b64() { printf "$1" | base64 -w0 | sed 's/+/%2B/g; s/\//%2F/g; s/=/%3D/g'; }`; `$H/raw?b=<b64>` replies with exactly those bytes (`&keep=1` keeps the connection).
- *(WSL, `python3 linux/srv.py 21150 21151 &`):* 
  `~/curlnet/curl -sS http://127.0.0.1:21199/ 2>/dev/full; echo $?`. Also `~/curlnet/curl http://127.0.0.1:21150/ok 2>/dev/full >/dev/null` (progress meter), `-v ... 2>/dev/full`, `--badopt 2>/dev/full`, `-w '%{stderr}x' -o /dev/null -s .../ok 2>/dev/full`. Script: `linux/abort.sh`.

Commands (run each with REF and with PORT):

```sh
~/curlnet/curl -sS http://127.0.0.1:21199/ 2>/dev/full; echo $?
~/curlnet/curl http://127.0.0.1:21150/ok 2>/dev/full >/dev/null
```

**Expected (real curl)**

- ignores the failed stderr write and exits with the transfer's code: 7, 0, 0, 2, 0.

**Actual (Curl)**

- bash prints `Aborted`, exit 134. Closed stderr (`2>&-`) and an EPIPE stderr do not crash.

**Suspected location in source**

- `Curl.Console/UnhandledExceptionGuard.cs:31-38`. The catch block writes `curl: (2) ...` to standardError with no protection. That second IOException escapes Main, and the runtime then fails while printing the crash to the same full stderr.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr008Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Cause: .NET's Unix console stream throws `IOException` on ENOSPC; the first standard error line threw out of the runner, `UnhandledExceptionGuard` wrote `curl: (2)` to the same full stream, that escaped `Main`, and the runtime aborted. Fix: `Program.Main` wraps standard error in the new `StandardErrorFailureIgnoringStream`, which ignores an `IOException` from any write or flush, as curl ignores a failed `fputs` to `stderr` (ADR-0815). `UnhandledExceptionGuard.cs` needed no change; `StandardErrorFailureIgnoringStream.cs`, `Program.cs` and `Curl.Console/CLAUDE.md` were added to `touches` (no task in Doing on `origin/work/dark-factory` names a Curl.Console file).
- Measured 2026-10-26 in WSL: real curl 8.18.0 (OpenSSL) at `/usr/bin/curl` against Curl published self-contained for linux-x64 (`dotnet publish Curl.Console -r linux-x64 --self-contained -p:PublishAot=false`), before the fix and after it. Nothing listens on 21199. The report's `/ok` server was replaced by `file:///tmp/bl3537.txt` (`hello\n`), which draws the same progress meter, `-v` and `-w` output to standard error with no server needed. Stdout was compared with `od -c`; no file is written and nothing goes on the wire in any variant.
  - a `curl -sS http://127.0.0.1:21199/ 2>/dev/full`: curl 7; Curl before 134, after 7.
  - b `curl file:///tmp/bl3537.txt 2>/dev/full >/dev/null`: curl 0; Curl before 134, after 0.
  - c `curl -v file:///tmp/bl3537.txt 2>/dev/full | od -c`: curl `hello\n`, 0; Curl before nothing on stdout and `Aborted`, after `hello\n`, 0.
  - d `curl --badopt 2>/dev/full`: curl 2; Curl before 134, after 2.
  - e `curl -w '%{stderr}x' -o /dev/null -s file:///tmp/bl3537.txt 2>/dev/full`: curl 0; Curl before 134, after 0.
  - f `curl -sS http://127.0.0.1:21199/ 2>&-`: curl 7; Curl 134 both before and after. The report says closed standard error did not crash; at this commit it does, before this change as after, so nothing that matched stopped matching. Filed as BL-3824 (descriptor 2 reused by the runtime; not an `IOException`).
- Focused tests in `Curl.Console.UnitTests/Cgr008Tests.cs`: a run whose standard error is a stream failing every write as ENOSPC returns the transfer's exit 7 (and the guard's exit 2 when the run throws). Before the fix that write threw out of the guard. Curl.Console.UnitTests: 2875 passed, 25 skipped; the full fast run passed.
- `Measure-CodeQuality.ps1 -Library Curl.Console`: 0 failing members. The change is platform-neutral (in-memory streams only), which covers CI on Linux and macOS.

## Log

- 2026-10-26: Created from CGR-008 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. Curl ignores a failed standard error write, so 2>/dev/full no longer aborts and every CGR-008 repro gives curl's exit code on Linux
