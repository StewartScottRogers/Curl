---
id: BL-3579
title: Fix CGR-050: A NUL byte in a config file: curl stops reading the line/file at the NUL, PORT keeps parsing (different requests, exit codes and diagnost...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/ConfigFileSyntax.cs, Curl.Cli.UnitTests/Cgr050Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3579 — Fix CGR-050: A NUL byte in a config file: curl stops reading the line/file at the NUL, PORT keeps parsing (different requests, exit codes and diagnost...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-050 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-050: A NUL byte in a config file: curl stops reading the line/file at the NUL, PORT keeps parsing (different requests, exit codes and diagnostics; UTF-16 `.curlrc` handled differently)

- **Severity:** Medium
- **Area:** config-glob-output, environment
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: [config-glob-output CGO-06] Confirmed on Windows and Linux.
- **Related existing finding (GF/AF):** GF-0069
- **Source findings:** `config-glob-output CGO-06`, `environment ENV-13`

**Reproduction**

- *Setup (config-glob-output):* E = `SCR\evidence\config-glob-output`. Windows server: `dotnet run E\srv\server.cs -- 20102 E\verify\log.txt` (listens on 127.0.0.1 and [::1], logs raw requests, replies `200 OK` with body `path=<request target>`; query keys `lm=` adds Last-Modified, `cd=`/`cdhex=` add Content-Disposition, `size=n` body of n `A`, `short=n` over-declares Content-Length, `status=n`). Linux: `python3 E/linux/server.py 20103 log`. `$U` = `http://127.0.0.1:20102` (20103 on Linux). Run each case in a fresh directory with HOME, USERPROFILE and APPDATA pointing at it and CURL_HOME/XDG_CONFIG_HOME unset. Verifier scripts: `E\verify\v1_stdin.ps1`, `v2_misc.ps1`, `v3_misc.ps1`, `v4_maxpath.ps1`, `E\verify\linux\vl.sh`.
- *Setup (environment):* EV = `SCR\evidence\environment`. Windows: `dotnet run EV\server.cs 21200` (echo server: returns the raw request as the body; `/bin` returns bytes 0..255) and `dotnet run EV\s2\server2.cs 21201` (same, used as a proxy). `EV\verify_windows.ps1` runs each case in isolated `aref`/`port` directories with HOME/USERPROFILE/APPDATA at an empty directory. Linux: `EV\linux\verify.sh` and `EV\linux\vtls.sh` (echo on 21210, proxy on 21211, `openssl s_server` on 21213).
- **[config-glob-output CGO-06]** A NUL byte in a config file: curl stops reading the file at the NUL, the port keeps parsing
  - run `curl -sS -K k.cfg`, where `k.cfg` holds one of these byte sequences:
    - n1: `url = $U/k70` NUL `junk`
    - n2: `header = "X-A: a` NUL `b"\nurl = $U/nb01\n`
    - n4: `url = "$U/nb04"` NUL `\nheader = X-Next:1\n`
    - Scripts: `E\verify\v2_misc.ps1` n1/n2/n4 and `vl.sh` n1. The original is `E\b_rc.ps1` nb01-nb05.
- **[environment ENV-13]** A config file containing NUL bytes (including a UTF-16 `.curlrc`) gives different diagnostics
  - A `.curlrc` in HOME with the bytes `header = "X-A: 1"\nbog\0us\nheader = "X-B: 2"\n`, then `curl -s http://127.0.0.1:21200/echo`
  - A UTF-16LE `.curlrc` holding `header = "X-A: 1"`
  - Verifier cases: v-rc-nul and v-rc-utf16.

Commands (run each with REF and with PORT):

```sh
# [config-glob-output CGO-06]
curl -sS -K k.cfg
E\verify\v2_misc.ps1
E\b_rc.ps1
# [environment ENV-13]
curl -s http://127.0.0.1:21200/echo
```

**Expected (real curl)**

- **[config-glob-output CGO-06]** ignores everything from the NUL to the end of the file.
  - n1 sends `GET /k70`, exit 0.
  - n2 prints `curl: (2) no URL specified`, exit 2.
  - n4 sends no X-Next header.
- **[environment ENV-13]** 
  - NUL case: `config file option 'bogheader' is unknown` (the NUL truncates the line and the next line is joined to it).
  - UTF-16 case: the file is ignored silently.

**Actual (Curl)**

- **[config-glob-output CGO-06]** 
  - n1: `curl: (3) URL rejected: Malformed input to a URL function`, exit 3.
  - n2: sends the request with `X-A: a b`, exit 0.
  - n4: sends `X-Next:1`.
- **[environment ENV-13]** 
  - NUL case: `config file option 'bog us' is unknown`.
  - UTF-16 case: an unquoted-whitespace warning, then `config file option 'ÿþh e a d e r ' is unknown`.
  - The request bytes are the same as REF in both cases.

**Suspected location in source**

- **[config-glob-output CGO-06]** `Curl.Cli.UnitLibrary/ConfigFileSyntax.cs:33`. Its doc comment says "Not modelled: a NUL byte ending a line early".
- **[environment ENV-13]** not located.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr050Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Plan (2026-10-26): `ConfigFileSyntax.ReadLines` now reads the bytes as curl 8.22.0's `Curl_get_line` does: `fgets` chunks of up to 127 bytes (128-byte buffer) or through the next LF, each kept up to its first NUL (`strlen`). A NUL drops the rest of its chunk with its LF, so the line runs on into the next one; a chunk that starts with a NUL stops the file silently, dropping the partial line (which is why a UTF-16LE `.curlrc` is ignored). CR LF is still read as LF first. No ADR: this is a measured match of curl, not a choice.
- Measured with the local curl 8.22.0 (mingw, Schannel) in Git Bash, `curl -sS -o NUL -K k.cfg file:///.../global.json`, write-out lines read back: `bog`NUL`us`LF`write-out = "C"` is `config file option 'bogwrite-out' is unknown` (also with CR LF); `"\0\n"` as a line stops the file with exit 0 and no message; a NUL 1 byte into a 300-byte parameter keeps 188 bytes (chunks of 127); a UTF-16LE file with an LF is ignored silently, one with no LF is the unknown option `ÿþh` (exit 2).
- Repro (Record-CurlExchange.ps1, loopback server on 20112, each side in its own HOME/USERPROFILE/APPDATA, real curl `C:\Program Files\Git\ucrt64\bin\curl.exe` against `Curl.Console\bin\Debug\net10.0\curl.exe`), after the fix, exit code, stdout, stderr and request bytes all equal:
  - n1 `url = $U/k70`NUL`junk`, `curl -sS -K k.cfg`: both exit 0, `GET /k70`, no stderr.
  - n2 `header = "X-A: a`NUL`b"\nurl = $U/nb01\n`: both exit 2, `curl: (2) no URL specified` and the try-help line, no request.
  - n4 `url = "$U/nb04"`NUL`\nheader = X-Next:1\n`: both exit 0, `GET /nb04` with no X-Next header.
  - ENV-13 NUL `.curlrc`, `curl -s $U/echo`: both exit 0, request with `X-A: 1` only, stderr `...\.curlrc:2 config file option 'bogheader' is unknown` (same apart from the per-side directory name).
  - ENV-13 UTF-16LE `.curlrc`: both exit 0, no stderr, request with no extra header.
  - Before the fix (from the issue): n1 exit 3, n2 sent `X-A: a b`, n4 sent X-Next, the NUL rc said `'bog us'`, the UTF-16 rc warned.
- Cgr050Tests: 9 of its 10 tests fail on the old `ConfigFileSyntax.cs` and all pass on the new one (the tenth pins lone CRs, which already matched). Full fast run: every project green apart from that one test, fixed and rerun green. Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary: 0 failing members.
- The Linux and macOS behaviour is the same source (`Curl_get_line`); the tests are platform-neutral (in-memory files, `isWindows: false`). CI on those platforms is left to the shift's CI watch.

## Log

- 2026-10-26: Created from CGR-050 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. A NUL byte in a config file or .curlrc now cuts its fgets chunk as curl 8.22.0 does, joining the next line and stopping at a chunk that starts with NUL, so UTF-16 .curlrc files are ignored
