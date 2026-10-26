---
id: BL-3568
title: Fix CGR-039: Linux: very large -m, --connect-timeout, --keepalive-time and --speed-time values fail with .NET exception text and exit 2
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr039Tests.cs, Curl.Console/EasyOptionsFromCommandLine.cs, Curl.Core.UnitLibrary/MaxTimeWatchdog.cs, Curl.Core.UnitTests/Cgr039Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3568 — Fix CGR-039: Linux: very large -m, --connect-timeout, --keepalive-time and --speed-time values fail with .NET exception text and exit 2

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-039 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-039: Linux: very large -m, --connect-timeout, --keepalive-time and --speed-time values fail with .NET exception text and exit 2

- **Severity:** Medium
- **Area:** cli-options
- **Platform(s):** Linux
- **Confidence:** Confirmed. Detail: Confirmed. The .NET exception text is a PORT defect whatever the REF version.
- **Related existing finding (GF/AF):** none
- **Source findings:** `cli-options CLIO-04`

**Reproduction**

- *Setup (cli-options):* Evidence `SCR\evidence\cli-options`. Windows: start the echo server `pwsh -NoProfile -File SCR\evidence\cli-options\echoserver.ps1 -Port 20000` (replies 200 with the raw request bytes as the body); `@U` = `http://127.0.0.1:20000/`; port 20002 is left closed. Harness: `SCR\evidence\cli-options\h.sh <name> <args>` runs REF and PORT and stores exit code, stdout, stderr and files. Linux: `python3 linux/echo.py 20001` (in the evidence folder); `URL` = `http://127.0.0.1:20001/`. Verifier scripts: `verify/v1_win.sh`, `verify/v4_win.sh`, `verify/v2_linux.sh`, `verify/v3_linux.sh`.
- *(WSL, echo.py on 20001):* `curl -s -m 4294968 -o /dev/null URL`; `--connect-timeout 4294968`; `--keepalive-time 922337203686`; `--speed-time 922337203686`. Scripts: `linux/t4.sh`, `verify/v2_linux.sh`.

Commands (run each with REF and with PORT):

```sh
curl -s -m 4294968 -o /dev/null URL
```

**Expected (real curl)**

- exit 0 and the transfer runs.

**Actual (Curl)**

- exit 2 before connecting. `-m` prints `curl: (2) dueTime ('4294968000') must be less than or equal to '4294967294'. (Parameter 'dueTime')` and `Actual value was 4294968000.`. `--connect-timeout` prints `curl: (2) Specified argument was out of the range of valid values. (Parameter 'delay')`. `--keepalive-time` and `--speed-time` print `curl: (2) TimeSpan overflowed because the duration is too long.`. `-m 4294967` works.

**Suspected location in source**

- `Curl.Core.UnitLibrary/MaxTimeWatchdog.cs:66`; `Curl.Console/EasyOptionsFromCommandLine.cs:239-240` and `:728`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr039Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-039 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
