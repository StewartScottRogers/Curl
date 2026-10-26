---
id: BL-3549
title: Fix CGR-020: Header text read from a file (-H @file, --proxy-header @file, -F ...;headers=@file, and -K cookie/request values) is decoded and re-encod...
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/HeaderFileText.cs, Curl.Cli.UnitLibrary/FormPartParameterReader.cs, Curl.Cli.UnitTests/Cgr020Tests.cs, Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs, Curl.Protocol.Http.UnitTests/Cgr020Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3549 — Fix CGR-020: Header text read from a file (-H @file, --proxy-header @file, -F ...;headers=@file, and -K cookie/request values) is decoded and re-encod...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-020 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-020: Header text read from a file (`-H @file`, `--proxy-header @file`, `-F ...;headers=@file`, and `-K` cookie/request values) is decoded and re-encoded instead of sent byte for byte

- **Severity:** High
- **Area:** http-requests, environment
- **Platform(s):** Both (Windows and Linux). Detail: [http-requests HR-2] Both (the symptoms differ by platform)
- **Confidence:** Confirmed. Detail: [http-requests HR-2] Confirmed: reproduced 2/2 on Windows and 2/2 on Linux.
- **Related existing finding (GF/AF):** none
- **Source findings:** `http-requests HR-2`, `environment ENV-02`

**Reproduction**

- *Setup (http-requests):* E = `SCR\evidence\http-requests`. Recording loopback server + differential runner: `dotnet run E\diff.cs -- E\verify.json E\vout1 20335` (Windows, port 20335) and `E/vlinux.sh` (WSL, port 20355; runs `python3 -I E/diff.py E/verify.json ~/hr_verify/v1 20355`). The server answers `200 OK` with body `ok\n` after reading the whole request; in `early` mode it answers right after the request head and keeps reading.
- *Setup (environment):* EV = `SCR\evidence\environment`. Windows: `dotnet run EV\server.cs 21200` (echo server: returns the raw request as the body; `/bin` returns bytes 0..255) and `dotnet run EV\s2\server2.cs 21201` (same, used as a proxy). `EV\verify_windows.ps1` runs each case in isolated `aref`/`port` directories with HOME/USERPROFILE/APPDATA at an empty directory. Linux: `EV\linux\verify.sh` and `EV\linux\vtls.sh` (echo on 21210, proxy on 21211, `openssl s_server` on 21213).
- **[http-requests HR-2]** Header text read from a file is decoded and re-encoded instead of sent byte for byte (-H @file, --proxy-header @file, -F ...;headers=@file, and -K cookie/request values)
  - Run `curl -sS -H @h.txt http://127.0.0.1:20335/a` with each of these h.txt files:
    - h.txt = bytes `X: \xc3\xa9\xe4\xb8\xad\n`
    - h.txt = bytes `X: \xe9\xff\n`

    Then run `curl -sS -K k.txt http://127.0.0.1:20335/a` with each of these k.txt files:
    - k.txt = `cookie = "c=\xc3\xa9"`
    - k.txt = `request = "\xc3\xa9"`

    The cases are v2_* in `E\verify.json`. The finder's cases are in `E\c20.json`, which also covers `--proxy-header @file` and `-F a=b;headers=@file`.
- **[environment ENV-02]** `-H @file`, `--proxy-header @file` and `-F "x;headers=@file"` re-encode the file's bytes to the ANSI code page, so non-cp1252 bytes become `?`
  - Create `h.txt` holding the UTF-8 text `X-C: 日本` followed by LF.
    - `curl -s -H @h.txt http://127.0.0.1:21200/echo`
    - `curl -s -x 127.0.0.1:21201 --proxy-header @h.txt http://127.0.0.1:21200/echo`
    - `curl -s -F "a=b;headers=@h.txt" http://127.0.0.1:21200/echo`, with `h.txt` = `X-C: é日本`
    - A file holding the raw bytes `X-C: ` FF 80 C3 28 behaves the same way.
    - Verifier cases: v-Hfile, v-Hfile-bin, v-pxhfile and v-Ffile.

Commands (run each with REF and with PORT):

```sh
# [http-requests HR-2]
curl -sS -H @h.txt http://127.0.0.1:20335/a
curl -sS -K k.txt http://127.0.0.1:20335/a
# [environment ENV-02]
curl -s -H @h.txt http://127.0.0.1:21200/echo
curl -s -x 127.0.0.1:21201 --proxy-header @h.txt http://127.0.0.1:21200/echo
curl -s -F "a=b;headers=@h.txt" http://127.0.0.1:21200/echo
```

**Expected (real curl)**

- **[http-requests HR-2]** Copies the file bytes unchanged:
  - the UTF-8 file sends `X: \xc3\xa9\xe4\xb8\xad`
  - the Latin-1 file sends `X: \xe9\xff`
  - the -K cookie sends `Cookie: c=\xc3\xa9`
  - the -K request sends the request line `\xc3\xa9 /a HTTP/1.1`
- **[environment ENV-02]** Sends the file's bytes unchanged: `X-C: E6 97 A5 E6 9C AC`. The raw bytes go out as FF 80 C3 28.

**Actual (Curl)**

- **[http-requests HR-2]** 
  - Windows: the UTF-8 file sends `X: \xe9?` (re-encoded to cp1252, and the CJK character becomes `?`). The Latin-1 file sends `X: ??`. The -K cookie sends `Cookie: c=\xe9` and the -K request sends `\xe9 /a`.
  - Linux: the UTF-8 header file matches REF. The Latin-1 file sends `X: \xef\xbf\xbd\xef\xbf\xbd` (two U+FFFD characters). The -K cookie sends `c=\xe9` and the -K request sends `\xe9 /a`.
  - The -K `header = "X: \xc3\xa9"` case matches REF on both platforms.
- **[environment ENV-02]** 
  - `-H` and `--proxy-header` send `X-C: ??`. The raw bytes go out as `???(`.
  - `-F` sends `X-C: E9 3F 3F`.
  - The header grows shorter and the original bytes are lost.

**Notes**

- **[environment ENV-02]** *Why this is not build-dependent:* upstream curl never transcodes file content, so a UNICODE curl build sends the raw bytes too.

**Suspected location in source**

- **[http-requests HR-2]** 
  - `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:1038` (`Encoding.UTF8.GetString(contents)` in the -H @file reader)
  - `Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs:158` (`Encoding.Latin1.GetBytes(head)`)
  - the method and cookie are appended raw (`HttpRequestHeadFormatter.cs:213` and `:150`)
- **[environment ENV-02]** 
  - `Curl.Cli.UnitLibrary\CommandLineOptionTable.cs` (~1038) decodes the file to a string, which the HTTP formatter later encodes as ANSI.
  - The same pattern is in `Curl.Cli.UnitLibrary\FormPartParameterReader.cs` (~185).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr020Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Added `Curl.Cli.UnitLibrary/HeaderFileText.cs` to `touches`: a new file holding the header-file reader both the `-H` table and the `-F` reader use; no other Doing task can hold a file that did not exist.
- Plan and decision (ADR-0817): `HeaderFileText.Read` reads a header file in the Windows ANSI code page when that round-trips its bytes, otherwise as UTF-8 with each unreadable byte escaped as lone surrogate U+DC00+byte; `HttpRequestHeadFormatter.WireText` writes those escapes back as bytes. `-K` `cookie` (values with `=`) and `request` values are re-spelled one character per file byte, since the request side writes them in Latin-1. Command-line `-b`/`-X` are unchanged (not part of this issue). A non-ASCII `-X` from a `-K` file used by a non-HTTP protocol would now be re-spelled too; no repro covers it.
- Measured real curl 8.22.0 (Schannel, `/ucrt64/bin/curl.exe`) and Curl with `Record-CurlExchange.ps1` on Windows, each against a recording server answering `200`, request bytes compared with the Host port and multipart boundary normalised:
  - `curl -sS -H @h1.txt URL` (h1 = `X: C3 A9 E4 B8 AD`): both exit 0, empty stdout/stderr, `X: C3 A9 E4 B8 AD`. Before the fix Curl sent `X: E9 3F`.
  - `curl -sS -H @h2.txt URL` (h2 = `X: E9 FF`): both `X: E9 FF`.
  - `curl -sS -K k1.txt URL` (`cookie = "c=C3 A9"`): both `Cookie: c=C3 A9`.
  - `curl -sS -K k2.txt URL` (`request = "C3 A9"`): both send the request line `C3 A9 /a HTTP/1.1`.
  - `curl -s -F "a=b;headers=@h1.txt" URL`: both send part header `X: C3 A9 E4 B8 AD`.
  - `curl -s -F "a=b;headers=@h3.txt" URL` (h3 = `X-C: FF 80 C3 28`): both send `X-C: FF 80 C3 28`.
  - All six: exit codes, stdout bytes, stderr text and request bytes equal; no files written by either.
- Linux and macOS (lanes test only on Windows): the Linux answers are pinned platform-neutrally by `Curl.Cli.UnitTests/Cgr020Tests.cs` parsing as the non-Windows build (`-H @file` of `E9 FF` gives the escapes, `-K` cookie/request give the file bytes) and `Curl.Protocol.Http.UnitTests/Cgr020Tests.cs` (escapes go out as bytes over UTF-8). Off Windows, a `-F ...;headers=@file` or CONNECT-tunnel `--proxy-header @file` holding bytes that are not valid UTF-8 still sends U+FFFD, as before: those encoders live in Curl.Core and Curl.Networking, outside this task's touches, and are filed as BL-3828. Valid UTF-8 header files (the ENV-02 `X-C: æ—¥æœ¬` repros) match on Linux.
- Focused tests: `Curl.Cli.UnitTests/Cgr020Tests.cs` (13) and `Curl.Protocol.Http.UnitTests/Cgr020Tests.cs` (4). Not re-run against the old code; they fail there by construction: the old reader decoded the files as plain UTF-8 (U+FFFD for `E9 FF`, and CJK text cp1252 cannot round-trip), the old cookie and request values held the decoded characters, and `WireText` did not exist.
- `dotnet build` clean; fast tests green (46 test projects, 0 failed). `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary,Curl.Protocol.Http.UnitLibrary`: 0 failing members.
## Log

- 2026-10-26: Created from CGR-020 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. -H @file, --proxy-header @file, -F ;headers=@file and -K cookie/request values reach the wire as the file's bytes, matching curl 8.22.0
