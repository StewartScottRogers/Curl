---
id: BL-3546
title: Fix CGR-017: Reading stdin twice in one command line (two @- consumers, -K - plus @-, or @- across --next) aborts with exit 2 and a .NET "Cannot acces...
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/DiskDataFileReader.cs, Curl.Cli.UnitTests/Cgr017Tests.cs, Curl.Cli.UnitTests/DiskDataFileReaderTests.cs, Curl.Console.UnitTests/Cgr017Tests.cs, Curl.Console/Program.cs, Curl.Console/UnhandledExceptionGuard.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3546 — Fix CGR-017: Reading stdin twice in one command line (two @- consumers, -K - plus @-, or @- across --next) aborts with exit 2 and a .NET "Cannot acces...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-017 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-017: Reading stdin twice in one command line (two `@-` consumers, `-K -` plus `@-`, or `@-` across `--next`) aborts with exit 2 and a .NET "Cannot access a closed Stream" message before sending anything

- **Severity:** High
- **Area:** config-glob-output, cli-options, http-requests, parallel-multi-transfer
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: [config-glob-output CGO-01] Confirmed. The verifier reproduced it on Windows (8.22) and Linux (8.18 agrees with 8.22).; [http-requests HR-1] Confirmed: reproduced 2/2 on Windows and 2/2 on Linux.; [parallel-multi-transfer PMT-1] Confirmed (Windows reproduced in the verifier run, Linux too)
- **Related existing finding (GF/AF):** GF-0007, GF-0082
- **Source findings:** `config-glob-output CGO-01`, `cli-options CLIO-03`, `http-requests HR-1`, `parallel-multi-transfer PMT-1`

All four areas trace this to one root cause: `Curl.Cli.UnitLibrary/DiskDataFileReader.cs` (`ReadStandardInput`, about lines 98-104) opens the process stdin with `using Stream standardInput = openStandardInput();`, so the first reader disposes it. curl reads stdin once and gives every later reader EOF (empty data).

**Reproduction**

- *Setup (config-glob-output):* E = `SCR\evidence\config-glob-output`. Windows server: `dotnet run E\srv\server.cs -- 20102 E\verify\log.txt` (listens on 127.0.0.1 and [::1], logs raw requests, replies `200 OK` with body `path=<request target>`; query keys `lm=` adds Last-Modified, `cd=`/`cdhex=` add Content-Disposition, `size=n` body of n `A`, `short=n` over-declares Content-Length, `status=n`). Linux: `python3 E/linux/server.py 20103 log`. `$U` = `http://127.0.0.1:20102` (20103 on Linux). Run each case in a fresh directory with HOME, USERPROFILE and APPDATA pointing at it and CURL_HOME/XDG_CONFIG_HOME unset. Verifier scripts: `E\verify\v1_stdin.ps1`, `v2_misc.ps1`, `v3_misc.ps1`, `v4_maxpath.ps1`, `E\verify\linux\vl.sh`.
- *Setup (cli-options):* Evidence `SCR\evidence\cli-options`. Windows: start the echo server `pwsh -NoProfile -File SCR\evidence\cli-options\echoserver.ps1 -Port 20000` (replies 200 with the raw request bytes as the body); `@U` = `http://127.0.0.1:20000/`; port 20002 is left closed. Harness: `SCR\evidence\cli-options\h.sh <name> <args>` runs REF and PORT and stores exit code, stdout, stderr and files. Linux: `python3 linux/echo.py 20001` (in the evidence folder); `URL` = `http://127.0.0.1:20001/`. Verifier scripts: `verify/v1_win.sh`, `verify/v4_win.sh`, `verify/v2_linux.sh`, `verify/v3_linux.sh`.
- *Setup (http-requests):* E = `SCR\evidence\http-requests`. Recording loopback server + differential runner: `dotnet run E\diff.cs -- E\verify.json E\vout1 20335` (Windows, port 20335) and `E/vlinux.sh` (WSL, port 20355; runs `python3 -I E/diff.py E/verify.json ~/hr_verify/v1 20355`). The server answers `200 OK` with body `ok\n` after reading the whole request; in `early` mode it answers right after the request head and keeps reading.
- *Setup (parallel-multi-transfer):* EV = `SCR\evidence\parallel-multi-transfer`. Server: `dotnet run EV\srv\server.cs -- 22000,22001 EV\srv\log` (listens on 127.0.0.1:22000 and :22001; query `d=` delay in ms, `s=` status, `n=` body size, `p=`/`pd=` send the body in parts with a delay, `close=1` adds Connection: close; other paths return `body /<path>`); port 22099 is closed. Linux: `EV/linux/srv.py 22000,22001 /tmp/pmt_srv` (started by `EV/linux/lt.sh`). Harness `EV\dt.ps1`; verifier `EV\v1.ps1` and `EV/linux/vl.sh`.
- **[config-glob-output CGO-01]** A second read of stdin makes the port abort with exit 2 and a .NET "Cannot access a closed Stream" message
  - each command below is run with the given text piped to stdin.
    - stdin `url = $U/s\n`:
      - `curl -sS -K - -d @-`
      - `curl -sS -K - -H @-`
      - `curl -sS -K - -K -`
    - stdin `X-H: 1\n`:
      - `curl -sS -d @- -H @- $U/s6`
      - `curl -sS -H @- -H @- $U/s7`
      - `curl -sS -d @- -d @- $U/s8`
      - `curl -sS -d @- $U/a --next -d @- $U/b`
    - Scripts: `E\verify\v1_stdin.ps1` (Windows) and `E\verify\linux\vl.sh` cases s1, s4 and s7 (Linux). The originals are `E\b_stdin.ps1` and `E\b_rc.ps1` (ks03, ks04).
- **[cli-options CLIO-03]** Reading stdin twice (two `@-` arguments) fails with .NET "Cannot access a closed Stream" and exit 2
  - `curl -s -d @- --data-binary @- @U < linux/stdin1.bin`; `curl -s -H @- -d @- @U < linux/stdin1.bin`. Scripts: `repro/r03_stdin_twice_win.sh`, `verify/v1_win.sh`, `verify/v3_linux.sh`.
- **[http-requests HR-1]** A second read of standard input makes Curl fail with exit 2 and print a .NET exception message
  - Any loopback server that answers 200, for example the harness. Run each of these:
    - `printf 'abc\n' | curl -sS -d @- -d @- http://127.0.0.1:20335/a`
    - `printf 'X-A: 1\n' | curl -sS -H @- -d @- http://127.0.0.1:20335/a`
    - `printf 'header = "X-K: 1"\n' | curl -sS -K - -d @- http://127.0.0.1:20335/a`
    - `printf abc | curl -sS -d @- http://127.0.0.1:20335/a --next -d @- http://127.0.0.1:20335/b`
    - `printf 'X-A: 1\n' | curl -sS -H @- -T - http://127.0.0.1:20335/a`

    The cases are v1_* in `E\verify.json`.
- **[parallel-multi-transfer PMT-1]** Reading stdin twice in one command line makes PORT fail with exit 2 and a .NET exception before it sends anything
  - start the server. Pipe `STDIN-DATA-123` (no newline) into each command:
    - `curl -s -d @- http://127.0.0.1:22000/a --next -d @- http://127.0.0.1:22000/b` (also the same command with `-Z`)
    - `curl -s -d @- -d @- http://127.0.0.1:22000/a`
    - `curl -s --json @- http://127.0.0.1:22000/a --next --json @- http://127.0.0.1:22000/b`
    - `curl -s -d @- http://127.0.0.1:22000/a --next -T - http://127.0.0.1:22000/b`
    - `curl -s -T - http://127.0.0.1:22000/a --next -d @- http://127.0.0.1:22000/b`

    Scripts: `v1.ps1` (v_d_twice, v_d_twice_Z, v_dd_same, v_json_next, v_d_then_T, v_T_then_d), `linux/vl.sh` (vl_d_twice). Older runs: `b9.ps1` and `b10.ps1`.

Commands (run each with REF and with PORT):

```sh
# [config-glob-output CGO-01]
curl -sS -K - -d @-
curl -sS -K - -H @-
curl -sS -K - -K -
curl -sS -d @- -H @- $U/s6
curl -sS -H @- -H @- $U/s7
curl -sS -d @- -d @- $U/s8
curl -sS -d @- $U/a --next -d @- $U/b
E\verify\v1_stdin.ps1
E\verify\linux\vl.sh
E\b_stdin.ps1
E\b_rc.ps1
# [cli-options CLIO-03]
curl -s -d @- --data-binary @- @U < linux/stdin1.bin
curl -s -H @- -d @- @U < linux/stdin1.bin
# [http-requests HR-1]
printf 'abc\n' | curl -sS -d @- -d @- http://127.0.0.1:20335/a
printf 'X-A: 1\n' | curl -sS -H @- -d @- http://127.0.0.1:20335/a
printf 'header = "X-K: 1"\n' | curl -sS -K - -d @- http://127.0.0.1:20335/a
printf abc | curl -sS -d @- http://127.0.0.1:20335/a --next -d @- http://127.0.0.1:20335/b
printf 'X-A: 1\n' | curl -sS -H @- -T - http://127.0.0.1:20335/a
# [parallel-multi-transfer PMT-1]
curl -s -d @- http://127.0.0.1:22000/a --next -d @- http://127.0.0.1:22000/b
curl -s -d @- -d @- http://127.0.0.1:22000/a
curl -s --json @- http://127.0.0.1:22000/a --next --json @- http://127.0.0.1:22000/b
curl -s -d @- http://127.0.0.1:22000/a --next -T - http://127.0.0.1:22000/b
curl -s -T - http://127.0.0.1:22000/a --next -d @- http://127.0.0.1:22000/b
```

**Expected (real curl)**

- **[config-glob-output CGO-01]** 
  - curl reads stdin once. Every later read gets EOF, which is empty data.
  - The transfer goes ahead with exit 0. Examples:
    - `-K - -d @-` sends `POST /s` with `Content-Length: 0`.
    - `-d @- -H @-` sends a POST whose body is `X-H: 1` and adds no extra header.
    - The `--next` case sends `POST /a` with the body, then `POST /b` with `Content-Length: 0`.
- **[cli-options CLIO-03]** exit 0. The first `@-` gets all of stdin and the second gets nothing. The body is `line1line2bin&` (Content-Length 14), and `-H @- -d @-` sends Content-Length 0.
- **[http-requests HR-1]** Exits 0 and sends the request. The first `@-` gets the stdin bytes and later reads get an empty string. `-d @- -d @-` sends `Content-Length: 4` with body `abc&`. `-H @- -d @-` sends `X-A: 1` with `Content-Length: 0`. `--next` sends POST /a with `abc`, then POST /b with an empty body. `-H @- -T -` sends `PUT` chunked with `0\r\n\r\n`.
- **[parallel-multi-transfer PMT-1]** exit 0. The first `@-` reads the 14 bytes and later reads get an empty body (POST /b with Content-Length: 0). stdout is `body /a\nbody /b\n`.

**Actual (Curl)**

- **[config-glob-output CGO-01]** 
  - No request is sent, not even the first transfer of a `--next` chain.
  - Exit 2, with this on stderr:
    ```
    curl: (2) Cannot access a closed Stream.
    Object name: 'WindowsConsoleStream'.
    ```
    On Linux the object name is `'UnixConsoleStream'`.
  - The original tester found two combinations that do not fail: `-T -` given twice, and `-F` with both `@-` and `<-`.
- **[cli-options CLIO-03]** exit 2 and no request is sent. stderr reads `curl: (2) Cannot access a closed Stream.` followed by `Object name: 'WindowsConsoleStream'.` (`'UnixConsoleStream'` on Linux).
- **[http-requests HR-1]** Exits 2, stdout is empty, and no request is sent. stderr is `curl: (2) Cannot access a closed Stream.` followed by `Object name: 'WindowsConsoleStream'.` (on Linux the object name is `'UnixConsoleStream'`). With `-H @- -T -` the port sends the request head with no body or terminating chunk, then prints `curl: (2) Stream does not support reading.` A single `-d @-` works and matches REF.
- **[parallel-multi-transfer PMT-1]** exit 2 and no request is sent. stderr is `curl: (2) Cannot access a closed Stream.` followed by `Object name: 'WindowsConsoleStream'.` (`'UnixConsoleStream'` on Linux). The `-d @-` / `-T -` combinations give `curl: (2) Stream does not support reading.` instead. For `-d @- … --next -T -`, the first transfer completes (`body /a`) before the failure.

**Suspected location in source**

- **[config-glob-output CGO-01]** 
  - `Curl.Cli.UnitLibrary/DiskDataFileReader.cs:98-104`: `ReadStandardInput` opens stdin with `using Stream standardInput = openStandardInput();`, so the process stdin stream is disposed after the first read.
  - The exception is turned into `curl: (2) <message>` at `Curl.Console/UnhandledExceptionGuard.cs:31-37`.
- **[cli-options CLIO-03]** `Curl.Cli.UnitLibrary/DiskDataFileReader.cs:100` (`using Stream standardInput = openStandardInput();` disposes the process stdin).
- **[http-requests HR-1]** `Curl.Cli.UnitLibrary/DiskDataFileReader.cs:98-103`. `ReadStandardInput` wraps the stream in `using Stream standardInput = openStandardInput();`, which disposes the process-wide stdin stream opened in `Curl.Console/Program.cs:19`. The next reader gets a closed stream.
- **[parallel-multi-transfer PMT-1]** `Curl.Cli.UnitLibrary/DiskDataFileReader.cs:98-103`. `ReadStandardInput` opens and disposes `Console.OpenStandardInput()` on every call. It is called from `CommandLineOptionTable.cs:716/887/1055`, so a second read hits the disposed console stream.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr017Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- 2026-10-26 (lane 7): Root cause confirmed: `DiskDataFileReader.ReadStandardInput` disposed the stream `openStandardInput` returned, which is the process stdin `Program.Main` owns, so every later read hit a closed stream. Fix: `ReadStandardInput` no longer disposes the stream (its owner does); a later read gets end of file, empty data, as curl does. Pipeline `feature` run as a direct change: a one-line root-cause fix needs no architect plan. No ADR: this matches curl, no design choice was made.
- Touches widened by `Curl.Cli.UnitTests/DiskDataFileReaderTests.cs`: its `ReadStandardInput_Always_ReturnsEveryByteAndClosesTheStream` pinned the bug, now `..._LeavesTheStreamOpen`. No task in Doing on origin/work/dark-factory names that file. `Curl.Console/*` and `CommandLineOptionTable.cs` needed no change.
- Focused test `Curl.Cli.UnitTests/Cgr017Tests.cs` reads stdin twice through one stream: before the fix the second read throws ObjectDisposedException, after it returns empty.
- Measured with Record-CurlExchange.ps1 (server `200 OK`, `Content-Length: 3`, `Connection: close`, two Response entries), REF curl 8.22.0 Schannel vs PORT `dotnet curl.dll`, host:port and User-Agent normalised. Exit code, stdout, stderr and request bytes all equal for: `-sS -d @- -d @- U/a` (stdin `abc\n`, exit 0, body `abc&`); `-sS -H @- -d @- U/a` (`X-A: 1\n`, 0, Content-Length 0); `-sS -H @- -H @- U/s7`; `-sS -d @- -H @- U/s6` (`X-H: 1\n`, 0); `-sS -K - -d @-`, `-K - -H @-`, `-K - -K -` (stdin `url = U/s\n`, 0, `POST /s` Content-Length 0); `-sS -d @- U/a --next -d @- U/b` (`abc`, 0); `-s -Z -d @- U/a --next -d @- U/b`, `-s --json @- U/a --next --json @- U/b`, `-s -d @- U/a --next -T - U/b`, `-s -T - U/a --next -d @- U/b` (`STDIN-DATA-123`, 0); `-sS -H @- -T - U/a` (`X-A: 1\n`, 0, PUT chunked `0\r\n\r\n`); `-s -d @- --data-binary @- U/` (`line1\nline2\n`, 0). Before the fix PORT gave exit 2 `Cannot access a closed Stream.` per the report.
- `-T -` twice across `--next` (noted by the report as not failing, not among the repros, not on the changed path) still exits 0 with equal stdout but differs on the wire: curl sends `abc` on the second PUT, Curl sends no body. Filed as BL-3827.
- Platform-neutral: the change and tests use only MemoryStream; no path, OS error or certificate. Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary: 0 failing members.

## Log

- 2026-10-26: Created from CGR-017 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. Reading stdin twice in one command line gives later readers empty data, as curl does, instead of exit 2
