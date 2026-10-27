---
id: BL-3717
title: Fix CGR-188: Windows: --create-dirs for an output path longer than MAX_PATH succeeds in PORT; REF fails with exit 23 before sending the request
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr188Tests.cs, Curl.Console/PhysicalOutputPaths.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3717 — Fix CGR-188: Windows: --create-dirs for an output path longer than MAX_PATH succeeds in PORT; REF fails with exit 23 before sending the request

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-188 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-188: Windows: `--create-dirs` for an output path longer than MAX_PATH succeeds in PORT; REF fails with exit 23 before sending the request

- **Severity:** Low
- **Area:** config-glob-output, environment
- **Platform(s):** Windows
- **Confidence:** Confirmed. Detail: [config-glob-output CGO-10] Confirmed on Windows.
- **Related existing finding (GF/AF):** none
- **Source findings:** `config-glob-output CGO-10`, `environment ENV-14`

**Reproduction**

- *Setup (config-glob-output):* E = `SCR\evidence\config-glob-output`. Windows server: `dotnet run E\srv\server.cs -- 20102 E\verify\log.txt` (listens on 127.0.0.1 and [::1], logs raw requests, replies `200 OK` with body `path=<request target>`; query keys `lm=` adds Last-Modified, `cd=`/`cdhex=` add Content-Disposition, `size=n` body of n `A`, `short=n` over-declares Content-Length, `status=n`). Linux: `python3 E/linux/server.py 20103 log`. `$U` = `http://127.0.0.1:20102` (20103 on Linux). Run each case in a fresh directory with HOME, USERPROFILE and APPDATA pointing at it and CURL_HOME/XDG_CONFIG_HOME unset. Verifier scripts: `E\verify\v1_stdin.ps1`, `v2_misc.ps1`, `v3_misc.ps1`, `v4_maxpath.ps1`, `E\verify\linux\vl.sh`.
- *Setup (environment):* EV = `SCR\evidence\environment`. Windows: `dotnet run EV\server.cs 21200` (echo server: returns the raw request as the body; `/bin` returns bytes 0..255) and `dotnet run EV\s2\server2.cs 21201` (same, used as a proxy). `EV\verify_windows.ps1` runs each case in isolated `aref`/`port` directories with HOME/USERPROFILE/APPDATA at an empty directory. Linux: `EV\linux\verify.sh` and `EV\linux\vtls.sh` (echo on 21210, proxy on 21211, `openssl s_server` on 21213).
- **[config-glob-output CGO-10]** --create-dirs succeeds past MAX_PATH where curl fails before sending the request (Windows)
  - from a working directory whose full path is 190 characters long, run `curl -sS --create-dirs -o "D/D/D/D/D/D/f" $U/x`, where each D is 50 `d` characters.
    - Script: `E\verify\v4_maxpath.ps1`. It runs the command twice; the result is the same both times.
- **[environment ENV-14]** `--create-dirs` for an output path longer than MAX_PATH succeeds in the port and fails with exit 23 in REF
  - `curl -sS --create-dirs -o aaaa(50)\aaaa(50)\aaaa(50)\aaaa(50)\aaaa(50)\bbbb(40).bin http://127.0.0.1:21200/bin`. That is five nested directories with 50-character names and a 40-character file name, about 470 characters as an absolute path. Verifier case: v-long-create.

Commands (run each with REF and with PORT):

```sh
# [config-glob-output CGO-10]
curl -sS --create-dirs -o "D/D/D/D/D/D/f" $U/x
E\verify\v4_maxpath.ps1
# [environment ENV-14]
curl -sS --create-dirs -o aaaa(50)\aaaa(50)\aaaa(50)\aaaa(50)\aaaa(50)\bbbb(40).bin http://127.0.0.1:21200/bin
```

**Expected (real curl)**

- **[config-glob-output CGO-10]** 
  - Creates only the first directory.
  - stderr: `curl: Error creating directory dddd.../dddd...` (wrapped over several lines), then `curl: (23) Failed writing received data to disk/application`, exit 23.
  - No request is sent.
- **[environment ENV-14]** `curl: Error creating directory ...` then `curl: (23) Failed writing received data to disk/application`. Exit 23 and no file is written.

**Actual (Curl)**

- **[config-glob-output CGO-10]** creates all six directories, sends `GET /x`, writes `f` and exits 0.
- **[environment ENV-14]** Creates the directories and writes the 256-byte file, exit 0.

**Suspected location in source**

- **[config-glob-output CGO-10]** `Curl.Console/PhysicalOutputPaths.cs:24`. `Directory.CreateDirectory` handles long paths.
- **[environment ENV-14]** not located.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr188Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-188 of the third-party gap report.
