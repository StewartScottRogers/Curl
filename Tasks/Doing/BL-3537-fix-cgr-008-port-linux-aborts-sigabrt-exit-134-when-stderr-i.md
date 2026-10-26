---
id: BL-3537
title: Fix CGR-008: PORT-LINUX aborts (SIGABRT, exit 134) when stderr is a full device (ENOSPC)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr008Tests.cs, Curl.Console/UnhandledExceptionGuard.cs]
requirement: none
created: 2026-10-26
completed:
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

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr008Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-008 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
