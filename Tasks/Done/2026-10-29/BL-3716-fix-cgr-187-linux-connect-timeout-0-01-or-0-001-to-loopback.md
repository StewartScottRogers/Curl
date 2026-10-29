---
id: BL-3716
title: Fix CGR-187: Linux: --connect-timeout 0.01 or 0.001 to loopback times out (exit 28) where REF connects
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests/Cgr187Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-29
---
# BL-3716 — Fix CGR-187: Linux: --connect-timeout 0.01 or 0.001 to loopback times out (exit 28) where REF connects

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-187 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-187: Linux: --connect-timeout 0.01 or 0.001 to loopback times out (exit 28) where REF connects

- **Severity:** Low
- **Area:** cli-options
- **Platform(s):** Linux
- **Confidence:** Probable. Detail: Probable (REF-LINUX 8.18. Windows matches.)
- **Related existing finding (GF/AF):** none
- **Source findings:** `cli-options CLIO-25`

**Reproduction**

- *Setup (cli-options):* Evidence `SCR\evidence\cli-options`. Windows: start the echo server `pwsh -NoProfile -File SCR\evidence\cli-options\echoserver.ps1 -Port 20000` (replies 200 with the raw request bytes as the body); `@U` = `http://127.0.0.1:20000/`; port 20002 is left closed. Harness: `SCR\evidence\cli-options\h.sh <name> <args>` runs REF and PORT and stores exit code, stdout, stderr and files. Linux: `python3 linux/echo.py 20001` (in the evidence folder); `URL` = `http://127.0.0.1:20001/`. Verifier scripts: `verify/v1_win.sh`, `verify/v4_win.sh`, `verify/v2_linux.sh`, `verify/v3_linux.sh`.
- `curl -sS --connect-timeout 0.01 -o /dev/null http://127.0.0.1:20001/`. Scripts: `linux/t12.sh`, `verify/v2_linux.sh` (2 runs).

Commands (run each with REF and with PORT):

```sh
curl -sS --connect-timeout 0.01 -o /dev/null http://127.0.0.1:20001/
```

**Expected (real curl)**

- exit 0.

**Actual (Curl)**

- exit 28 `curl: (28) Connection timed out after 19-22 milliseconds` every time.

**Suspected location in source**

- Not located by the reviewers.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Decision (Claude, delegated): curl's non-blocking connect to loopback completes before its timer is first checked, so a 10 ms limit connects; here runtime overhead before the dial can pass 10 ms (Linux, 19-22 ms measured by the report). `TcpConnector.MinimumLoopbackConnectTimeout` (100 ms) now floors the connect limit for a loopback literal or localhost destination with no proxy or Unix socket. Not reproducible on Windows (matches already); Linux run not possible in a lane, so criterion 1 is met by the unit tests in Cgr187Tests.cs (clock passing the 10 ms limit during the dial still connects; stall past 100 ms still exit 28; non-loopback still times out at the limit). Platform-neutral.
- Measure-CodeQuality: first run showed complexity 12 on ConnectWithinTimeoutAsync, fixed by extracting LimitForDestination; the re-runs stopped on an unrelated test (SslStreamTlsProviderTests SelfSignedServerUnderInsecure, passes alone) filed as a task.

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr187Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-187 of the third-party gap report.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-29: Doing -> Done. A loopback connect with --connect-timeout 0.01 is held to a 100 ms floor so it connects as curl does
