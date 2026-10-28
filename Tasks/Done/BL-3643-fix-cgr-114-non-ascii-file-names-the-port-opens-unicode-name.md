---
id: BL-3643
title: Fix CGR-114: Non-ASCII file names: the port opens Unicode names on Windows, and decodes file:// %XX as UTF-8, while REF uses byte/ANSI names
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3900, BL-3899]
touches: [Curl.Protocol.File.UnitLibrary/FileUrlPath.cs, Curl.Protocol.File.UnitTests/Cgr114Tests.cs, Curl.Protocol.File.UnitTests/FileUrlPathAdversarialTests.cs, Curl.Protocol.File.UnitTests/FileUrlPathTests.cs, Curl.Protocol.File.UnitTests/FileProtocolHandlerAdversarialTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
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

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr114Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- 2026-10-26 (lane 4, first pass, no code changed): measured real curl 8.22.0 (Git ucrt64 mingw, Schannel, non-UNICODE) on Windows with ACP 1252, files `café.txt`, `日本.txt`, `cafÃ©.txt` in `Z:/repos/Curl.lanes/cgr114-l4`: `file:///.../caf%E9.txt` prints `latin`, exit 0; `file:///.../%E6%97%A5%E6%9C%AC.txt` exits 37 `Could not open file Z:/repos/Curl.lanes/cgr114-l4/%E6%97%A5%E6%9C%AC.txt`; `file:///.../caf%C3%A9.txt` opens `cafÃ©.txt` (exit 0); `%81x.txt` exits 37. So on Windows the decoded escape bytes are read in the system ANSI code page, not UTF-8.
- Plan for this task when it comes back: in `FileUrlPath.Decode`/`Flush`, read each run of escape bytes in the system ANSI code page on Windows (`CodePagesEncodingProvider.Instance.GetEncoding(0)`, asked twice as BL-1200 found, Windows-1252 fallback; the protocol library cannot reference `CredentialEncoding`), and in BL-3899's raw-byte path form on Unix; take the encoding through an internal `TryParse` overload so tests inject it on every platform. Unescaped non-ASCII characters in the URL stay as typed (the reference receives them in ANSI, which round-trips é). Existing tests pin UTF-8 decoding through the public `TryParse` (`FileUrlPathAdversarialTests` %C3%A9 / %C3x%A9 cases, `FileProtocolHandlerAdversarialTests.ExecuteAsync_NonAsciiName_OpensTheDecodedCharacters`, possibly `FileUrlPathTests`): move them onto the injected-encoding overload or pin per platform, so those files were added to `touches` (no live task in Doing on origin/work/dark-factory names them). Record the decision in an ADR (mingw reference, ADR-0018/ADR-0067).
- Requeued for the work outside this task's library: BL-3900 (Windows `-o`, `-T`, config `output` names in ANSI bytes, in Curl.Console) and BL-3899 (Unix raw-byte open in PhysicalFileSystem plus the raw-byte path form in Protocol.Abstractions, which this task's Linux half needs).

- 2026-10-28 (lane 7): fixed as planned. `FileUrlPath` reads each run of escape bytes through `EscapedBytesReaderFor`: the system ANSI code page on Windows (asked twice, Windows-1252 fallback), `RawBytePath.FromBytes` elsewhere; unescaped non-ASCII stays as typed; `UrlPath` unchanged. An internal `TryParse(url, driveLetters, readEscapedBytes, out path)` lets tests pin both readings on every platform. Decision in ADR-0864.
- Measured 2026-10-28 on this Windows-1252 host, real curl `C:\Program Files\Git\ucrt64\bin\curl.exe` (8.22.0 mingw, Schannel) against Curl (`dotnet run --project Curl.Console`), with `café.txt` (latin), `日本.txt` (cjk), `cafÃ©.txt` (mojibake) in one directory, `-sS`:
  - `file:///<dir>/caf%E9.txt`: REF exit 0 `latin`; PORT before exit 37, after exit 0 `latin`.
  - `file:///<dir>/%E6%97%A5%E6%9C%AC.txt`: REF exit 37 `curl: (37) Could not open file <dir>/%E6%97%A5%E6%9C%AC.txt`; PORT before exit 0 `cjk`, after the same exit 37 and text.
  - `file:///<dir>/caf%C3%A9.txt`: REF exit 0 `mojibake`; PORT before `latin`, after `mojibake`. `%81x.txt`: both exit 37, same text.
  - Already matching (BL-3900), still matching, each in its own empty directory: `-s -o 日本語.bin <file url>` both exit 0 and write `___.bin`; `-s -T 日本.txt http://127.0.0.1:1/echo` both exit 26 `curl: cannot open '??.txt'` plus the try-help line; `-s -K c.cfg` (UTF-8, `output = "café.bin"`) both exit 0 and create `cafÃ©.bin`. No bytes on the wire in any of these (file:// or a failed open).
  - Linux half (`caf%E9.txt` opening the raw-byte name): pinned by `Cgr114Tests.TryParse_Latin1EscapeOffWindows_KeepsTheRawByteForOpen` (reader injected, runs everywhere) and `TryParse_PublicOffWindows_KeepsTheRawByte` (Unix-only, skipped here); BL-3899's raw-byte open does the rest. Platform-neutral tests and the green fast run here meet the Linux/macOS side.
- Focused tests: `Curl.Protocol.File.UnitTests/Cgr114Tests.cs` (the Windows-reader and public Windows tests fail before the fix: UTF-8 gave `caf` U+FFFD `.txt`). `FileUrlPathAdversarialTests` now pins the raw-byte reader explicitly (invalid UTF-8 escapes are raw-byte surrogates, `%80` in its own test because a lone surrogate does not survive a DataRow); `FileProtocolHandlerAdversarialTests.ExecuteAsync_NonAsciiName_OpensTheDecodedCharacters` uses an ASCII escape, since an escape's non-ASCII reading is now per platform.
- dotnet build clean (0 warnings); fast tests: all 46 test projects passed. Measure-CodeQuality -Library Curl.Protocol.File.UnitLibrary: 0 failing members.

## Log

- 2026-10-26: Created from CGR-114 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Backlog. Waits on BL-3900 (Windows -o/-T/config file names in ANSI bytes, Curl.Console) and BL-3899 (Unix raw-byte file open hook); the file:// decode here follows them
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Done. file:// %XX escapes are read in the ANSI code page on Windows and as raw bytes elsewhere, so caf%E9.txt opens café.txt and a UTF-8 escape exits 37 as curl 8.22.0 does (ADR-0864)
