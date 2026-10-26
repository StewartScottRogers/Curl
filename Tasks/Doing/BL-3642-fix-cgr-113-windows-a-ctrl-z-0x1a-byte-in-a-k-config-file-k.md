---
id: BL-3642
title: Fix CGR-113: Windows: a Ctrl-Z (0x1A) byte in a -K config file, -K - or .curlrc ends the file for curl; PORT parses past it (unknown option, exit 2)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/ConfigFileSyntax.cs, Curl.Cli.UnitTests/Cgr113Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3642 — Fix CGR-113: Windows: a Ctrl-Z (0x1A) byte in a -K config file, -K - or .curlrc ends the file for curl; PORT parses past it (unknown option, exit 2)

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-113 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-113: Windows: a Ctrl-Z (0x1A) byte in a `-K` config file, `-K -` or `.curlrc` ends the file for curl; PORT parses past it (unknown option, exit 2)

- **Severity:** Medium
- **Area:** environment, config-glob-output
- **Platform(s):** Windows
- **Confidence:** Confirmed. Detail: [config-glob-output CGO-07] Confirmed on Windows. On Linux, REF 8.18 also treats 0x1A as content and fails (`vl.sh` z1), so this is Windows-only.
- **Related existing finding (GF/AF):** none
- **Source findings:** `environment ENV-06`, `config-glob-output CGO-07`

**Reproduction**

- *Setup (environment):* EV = `SCR\evidence\environment`. Windows: `dotnet run EV\server.cs 21200` (echo server: returns the raw request as the body; `/bin` returns bytes 0..255) and `dotnet run EV\s2\server2.cs 21201` (same, used as a proxy). `EV\verify_windows.ps1` runs each case in isolated `aref`/`port` directories with HOME/USERPROFILE/APPDATA at an empty directory. Linux: `EV\linux\verify.sh` and `EV\linux\vtls.sh` (echo on 21210, proxy on 21211, `openssl s_server` on 21213).
- *Setup (config-glob-output):* E = `SCR\evidence\config-glob-output`. Windows server: `dotnet run E\srv\server.cs -- 20102 E\verify\log.txt` (listens on 127.0.0.1 and [::1], logs raw requests, replies `200 OK` with body `path=<request target>`; query keys `lm=` adds Last-Modified, `cd=`/`cdhex=` add Content-Disposition, `size=n` body of n `A`, `short=n` over-declares Content-Length, `status=n`). Linux: `python3 E/linux/server.py 20103 log`. `$U` = `http://127.0.0.1:20102` (20103 on Linux). Run each case in a fresh directory with HOME, USERPROFILE and APPDATA pointing at it and CURL_HOME/XDG_CONFIG_HOME unset. Verifier scripts: `E\verify\v1_stdin.ps1`, `v2_misc.ps1`, `v3_misc.ps1`, `v4_maxpath.ps1`, `E\verify\linux\vl.sh`.
- **[environment ENV-06]** A Ctrl-Z (0x1A) byte in a `-K` config file (or `-K -`) ends the file in REF on Windows; the port reports an unknown option and exits 2
  - `c.cfg` holds the bytes `header = "X-A: 1"\r\n\x1A header = "X-C: 3"\r\n`. Run `curl -s -K c.cfg http://127.0.0.1:21200/echo`.
  - The same bytes on stdin with `-K -` behave the same way.
  - Verifier cases: v-K-ctrlz and v-K-ctrlz-stdin.
- **[config-glob-output CGO-07]** Windows: a Ctrl-Z (0x1A) byte in a config file or .curlrc ends the file for curl, but the port parses it as content
  - z1: `k.cfg` = `url = $U/nb03\n` 0x1A `\nheader = X-After-Ctrl-Z:1\n`, then `curl -sS -K k.cfg`.
  - z2: `%HOME%\.curlrc` = `header = X-Before:1\n` 0x1A `header = X-After:1\n`, then `curl -sS $U/rcz`.
  - Script: `E\verify\v2_misc.ps1` z1/z2.

Commands (run each with REF and with PORT):

```sh
# [environment ENV-06]
curl -s -K c.cfg http://127.0.0.1:21200/echo
# [config-glob-output CGO-07]
curl -sS -K k.cfg
curl -sS $U/rcz
E\verify\v2_misc.ps1
```

**Expected (real curl)**

- **[environment ENV-06]** Applies X-A, treats 0x1A as end of file, exits 0.
- **[config-glob-output CGO-07]** stops reading at 0x1A (Windows text-mode EOF).
  - z1 sends `GET /nb03`, exit 0.
  - z2 sends X-Before only and prints nothing on stderr.

**Actual (Curl)**

- **[environment ENV-06]** Prints `curl: option -K: found an unknown config option` and `curl: try 'curl --help' or 'curl --manual' for more information`. Exits 2 and sends nothing.
- **[config-glob-output CGO-07]** 
  - z1: no request is sent, exit 2, with stderr:
    ```
    curl: k.cfg:2 config file option '' is unknown
    curl: option -K: found an unknown config option
    ```
  - z2: prints a warning that `.curlrc:2 config file option 'header' is unknown`, then continues.

**Suspected location in source**

- **[environment ENV-06]** the config-file reader in `Curl.Cli.UnitLibrary` has no text-mode end-of-file handling.
- **[config-glob-output CGO-07]** `Curl.Cli.UnitLibrary/ConfigFileSyntax.cs`. The line reader models the CRLF part of a text-mode read (line 14) but not the Ctrl-Z EOF.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr113Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-113 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
