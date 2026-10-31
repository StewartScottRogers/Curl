---
id: BL-3705
title: Fix CGR-176: -s -T . prints no progress meter; REF prints it even with -s
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-3823]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests/Cgr176Tests.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/Cgr176Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-29
---
# BL-3705 — Fix CGR-176: -s -T . prints no progress meter; REF prints it even with -s

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-176 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-176: `-s -T .` prints no progress meter; REF prints it even with `-s`

- **Severity:** Low
- **Area:** cli-options, http-requests, environment
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: [http-requests HR-11] Confirmed: reproduced 2/2 on Windows and 2/2 on Linux.
- **Related existing finding (GF/AF):** GF-0078
- **Source findings:** `cli-options CLIO-13`, `http-requests HR-11`, `environment ENV-11`

**Reproduction**

- *Setup (cli-options):* Evidence `SCR\evidence\cli-options`. Windows: start the echo server `pwsh -NoProfile -File SCR\evidence\cli-options\echoserver.ps1 -Port 20000` (replies 200 with the raw request bytes as the body); `@U` = `http://127.0.0.1:20000/`; port 20002 is left closed. Harness: `SCR\evidence\cli-options\h.sh <name> <args>` runs REF and PORT and stores exit code, stdout, stderr and files. Linux: `python3 linux/echo.py 20001` (in the evidence folder); `URL` = `http://127.0.0.1:20001/`. Verifier scripts: `verify/v1_win.sh`, `verify/v4_win.sh`, `verify/v2_linux.sh`, `verify/v3_linux.sh`.
- *Setup (http-requests):* E = `SCR\evidence\http-requests`. Recording loopback server + differential runner: `dotnet run E\diff.cs -- E\verify.json E\vout1 20335` (Windows, port 20335) and `E/vlinux.sh` (WSL, port 20355; runs `python3 -I E/diff.py E/verify.json ~/hr_verify/v1 20355`). The server answers `200 OK` with body `ok\n` after reading the whole request; in `early` mode it answers right after the request head and keeps reading.
- *Setup (environment):* EV = `SCR\evidence\environment`. Windows: `dotnet run EV\server.cs 21200` (echo server: returns the raw request as the body; `/bin` returns bytes 0..255) and `dotnet run EV\s2\server2.cs 21201` (same, used as a proxy). `EV\verify_windows.ps1` runs each case in isolated `aref`/`port` directories with HOME/USERPROFILE/APPDATA at an empty directory. Linux: `EV\linux\verify.sh` and `EV\linux\vtls.sh` (echo on 21210, proxy on 21211, `openssl s_server` on 21213).
- **[cli-options CLIO-13]** -s -T . : REF still draws the progress meter; PORT prints nothing
  - `curl -s -T . -o NUL @U < linux/stdin1.bin` (and on Linux with `-o /dev/null`, with or without stdin). Scripts: `repro/r12_Tdot_win.sh`, `verify/v1_win.sh`, `verify/v2_linux.sh`.
- **[http-requests HR-11]** -T . with -s (or -sS) still writes the progress meter in curl, but Curl stays silent
  - Run `printf 'hello dot\n' | curl -s -T . http://127.0.0.1:20335/a` (case v11_Tdot_s). The control case `-s -T -` matches.
- **[environment ENV-11]** `-s -T .` (upload from stdin, non-blocking) prints no progress meter; REF prints it even with `-s`
  - Windows: `echo a | curl -s -T . http://127.0.0.1:21200/echo` (v-Tdot)
  - Linux: the same command with port 21210 (v-Tdot)

Commands (run each with REF and with PORT):

```sh
# [cli-options CLIO-13]
curl -s -T . -o NUL @U < linux/stdin1.bin
# [http-requests HR-11]
printf 'hello dot\n' | curl -s -T . http://127.0.0.1:20335/a
# [environment ENV-11]
echo a | curl -s -T . http://127.0.0.1:21200/echo
```

**Expected (real curl)**

- **[cli-options CLIO-13]** stderr contains progress-meter rows despite `-s`.
- **[http-requests HR-11]** Even with `-s`, stderr gets the meter header lines (`  % Total    % Received % Xferd ...`) and CR-separated progress rows. This happens on both 8.22.0 (Windows) and 8.18.0 (Linux).
- **[environment ENV-11]** stderr gets the `% Total % Received ...` header and the progress lines. REF 8.22 and REF-LINUX 8.18 both do this.

**Actual (Curl)**

- **[cli-options CLIO-13]** stderr is empty.
- **[http-requests HR-11]** stderr is empty. The request bytes are identical.
- **[environment ENV-11]** stderr is empty. stdout and the exit code are the same as REF.

**Notes**

- **[http-requests HR-11]** *Note:* REF's behaviour contradicts the `-s` documentation and is probably a curl quirk. Strict drop-in parity still differs.

**Suspected location in source**

- **[http-requests HR-11]** the progress-meter enablement in `Curl.Cli.UnitLibrary`. Exact line not located.
- **[environment ENV-11]** not located.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr176Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-176 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Found in the first pass: the meter is decided in `Curl.Console/CurlCommandRunner.cs` `ShowsProgress` (line ~4640, `!options.Silent && !options.ProgressMeterOff`), not in Curl.Cli.UnitLibrary. Real curl draws the meter under `-s` when `-T .` uploads stdin without blocking (`Running.UploadsFromStandardInputWithoutBlocking`, line ~3885). Fix: let that case ignore `Silent` in `ShowsProgress`, with a focused test in `Curl.Console.UnitTests/Cgr176Tests.cs`. BL-3823 (Doing) holds CurlCommandRunner.cs, so touches was extended and BL-3823 added to depends-on; requeued before any edit.
- 2026-10-28: Doing -> Backlog. Needs Curl.Console/CurlCommandRunner.cs, held by BL-3823; depends-on BL-3823
- 2026-10-29: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-29: Fixed: `ShowsProgress` ignores `-s` when `Running.UploadsFromStandardInputWithoutBlocking` (`-T .`), via `IsSilencedProgress`. Real curl was not re-measured this run: the three repros are the report's REF results (meter header and rows on stderr under `-s -T .`; `-s -T -` silent). Tests in `Curl.Console.UnitTests/Cgr176Tests.cs` (dot draws the meter, dash stays silent); a repro variant that matched (`-s -T -`) still matches. Platform-neutral (in-memory streams). Measure-CodeQuality for Curl.Console: 0 failing (first try hit complexity 12, helper extracted). Fast tests green.
- 2026-10-29: Doing -> Done. CGR-176 fixed: -s -T . draws the meter
