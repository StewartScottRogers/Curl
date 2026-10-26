---
id: BL-3643
title: Fix CGR-114: Non-ASCII file names: the port opens Unicode names on Windows, and decodes file:// %XX as UTF-8, while REF uses byte/ANSI names
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.File.UnitLibrary/FileUrlPath.cs, Curl.Protocol.File.UnitTests/Cgr114Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3643 — Fix CGR-114: Non-ASCII file names: the port opens Unicode names on Windows, and decodes file:// %XX as UTF-8, while REF uses byte/ANSI names

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-114 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-114: Non-ASCII file names: the port opens Unicode names on Windows, and decodes file:// `%XX` as UTF-8, while REF uses byte/ANSI names

- **Severity:** Medium
- **Area:** environment
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `environment ENV-08`

**Reproduction**

- *Setup (environment):* EV = `SCR\evidence\environment`. Windows: `dotnet run EV\server.cs 21200` (echo server: returns the raw request as the body; `/bin` returns bytes 0..255) and `dotnet run EV\s2\server2.cs 21201` (same, used as a proxy). `EV\verify_windows.ps1` runs each case in isolated `aref`/`port` directories with HOME/USERPROFILE/APPDATA at an empty directory. Linux: `EV\linux\verify.sh` and `EV\linux\vtls.sh` (echo on 21210, proxy on 21211, `openssl s_server` on 21213).
- *(Windows):* 
  - `curl -s -o 日本語.bin http://127.0.0.1:21200/bin`
  - `curl -s -T 日本.txt http://127.0.0.1:21200/echo`
  - A config file containing `output = "café.bin"`, saved as UTF-8, then `curl -s -K c.cfg http://127.0.0.1:21200/bin`
  - `curl -s file:///<dir>/caf%E9.txt` where `café.txt` exists
  - `curl -s file:///<dir>/%E6%97%A5%E6%9C%AC.txt` where `日本.txt` exists
  - Verifier cases: v-o-cjk, v-T-cjk, v-cfg-o-latin, v-file-latin1 and v-file-utf8pct.
- *(Linux):* create a file whose name is the raw bytes `caf\xE9.txt`, then run `~/curlnet/curl -sS file://$PWD/caf%E9.txt` (`linux/verify.sh` v-file-latin1-pct).

Commands (run each with REF and with PORT):

```sh
curl -s -o 日本語.bin http://127.0.0.1:21200/bin
curl -s -T 日本.txt http://127.0.0.1:21200/echo
curl -s -K c.cfg http://127.0.0.1:21200/bin
curl -s file:///<dir>/caf%E9.txt
curl -s file:///<dir>/%E6%97%A5%E6%9C%AC.txt
~/curlnet/curl -sS file://$PWD/caf%E9.txt
```

**Expected (real curl)**

- *REF (Windows):* 
  - `-o` writes `___.bin`.
  - `-T` fails with `curl: cannot open '??.txt'`, exit 26.
  - The config output creates `cafÃ©.bin`.
  - `caf%E9.txt` opens `café.txt`.
  - The UTF-8 `%`-escape fails with exit 37.
- *REF (Linux):* `caf%E9` opens the raw-byte name, exit 0.

**Actual (Curl)**

- *PORT (Windows):* 
  - `-o` writes `日本語.bin`.
  - `-T` succeeds.
  - The config output creates `café.bin`.
  - `caf%E9.txt` fails with exit 37.
  - The UTF-8 `%`-escape opens `日本.txt`.
- *PORT (Linux):* fails with `curl: (37) Could not open file .../caf%E9.txt`.

**Notes**

- *Build note:* on Windows the port's behaviour is the same as a UNICODE curl build. `C:\Windows\System32\curl.exe` 8.21 also writes `日本語.bin`, fails on `caf%E9.txt` (37) and opens the UTF-8 escape. So the Windows half differs only from the non-UNICODE REF build. The Linux half is a real difference: upstream curl passes the decoded bytes to `open()` unchanged.

**Suspected location in source**

- `Curl.Protocol.File.UnitLibrary\FileUrlPath.cs:270-300` (`Decode`/`Flush` decode `%XX` as UTF-8). The .NET wide-character file APIs are used for `-o`, `-T` and `-K`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr114Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-114 of the third-party gap report.
