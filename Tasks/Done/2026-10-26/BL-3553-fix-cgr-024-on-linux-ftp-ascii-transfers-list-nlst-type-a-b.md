---
id: BL-3553
title: Fix CGR-024: On Linux, FTP ASCII transfers (LIST, NLST, ;type=a, -B) keep CRLF; curl converts CRLF and lone CR to LF on non-Windows platforms
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests/Cgr024Tests.cs, Curl.Protocol.Ftp.UnitTests/Fakes/FtpAsciiOutput.cs, Curl.Protocol.Ftp.UnitTests/FtpProtocolHandlerGracefulDataCloseTests.cs, Curl.Protocol.Ftp.UnitTests/FtpProtocolHandlerListingRequestTests.cs, Curl.Protocol.Ftp.UnitTests/FtpProtocolHandlerRangeTests.cs, Curl.Protocol.Ftp.UnitTests/FtpProtocolHandlerSizeDownloadTests.cs, Curl.Protocol.Ftp.UnitTests/FtpProtocolHandlerTests.cs, Curl.Protocol.Ftp.UnitTests/FtpProtocolHandlerTransferTypeTests.cs, Curl.Protocol.Ftp.UnitTests/FtpProtocolHandlerWildcardTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3553 — Fix CGR-024: On Linux, FTP ASCII transfers (LIST, NLST, ;type=a, -B) keep CRLF; curl converts CRLF and lone CR to LF on non-Windows platforms

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-024 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-024: On Linux, FTP ASCII transfers (LIST, NLST, `;type=a`, `-B`) keep CRLF; curl converts CRLF and lone CR to LF on non-Windows platforms

- **Severity:** High
- **Area:** ftp-file-tftp
- **Platform(s):** Linux
- **Confidence:** Confirmed. Detail: Confirmed. REF-LINUX is 8.18, but this is not a version difference: in the 8.22 source tree, `lib/urldata.h` defines `CURL_PREFER_LF_LINEENDS` on `!_WIN32`, and `lib/ftp.c` `ftp_cw_lc_write` (around line 359) does the conversion.
- **Related existing finding (GF/AF):** none
- **Source findings:** `ftp-file-tftp FTP-3`

**Reproduction**

- *Setup (ftp-file-tftp):* EV = `SCR\evidence\ftp-file-tftp`. Scripted FTP/TFTP stub: `EV\stub\win3\stub.exe` (Linux `EV/stub/linux/stub`, source `EV\stub\Program.cs`) with fixture root `EV\fixture\root` (file.txt is 18 bytes). Differential harness `EV\dt.sh NAME 'script' curl-args`, where `@U@` = `ftp://127.0.0.1:$CPORT`, script lines are `CMD=reply` or `OPT key=value` separated by `;;`, and `STUBEXTRA=EV\tls.pfx` enables FTPS. Verifier: `EV\v1.sh`, `EV\v2.sh` (Windows), `EV\vl.sh` (Linux).
- WSL, with the stub's default LIST lines ending in CRLF. Commands:
  - `curl -sS ftp://127.0.0.1:20986/dir/`
  - `curl -sS 'ftp://127.0.0.1:20986/crlf.txt;type=a'`
  - script `LIST.data=a\rb\r\n` with `curl -sS ftp://127.0.0.1:20986/dir/`

  Cases: `v_list`, `v_typea`, `v_lonecr` (`EV\vl.sh`); originals under `EV\cases-linux\` (`list_dir`, `nlst`, `l_*`, `ascii_dl`, ...).

Commands (run each with REF and with PORT):

```sh
curl -sS ftp://127.0.0.1:20986/dir/
curl -sS 'ftp://127.0.0.1:20986/crlf.txt;type=a'
EV\vl.sh
```

**Expected (real curl)**

- Lines end in LF: `...d.txt\n-rw-r--r-- ...`; `a\nb\nc\n`; `a\nb\n`.

**Actual (Curl)**

- The bytes are passed through unchanged: `...d.txt\r\n...`; `a\r\nb\r\nc\r\n`; `a\rb\r\n`. Binary transfers (`v_bin`) match.

**Suspected location in source**

- Not implemented in `Curl.Protocol.Ftp.UnitLibrary` (there is no line-end conversion writer).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr024Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Touches widened (2026-10-26): added `Curl.Protocol.Ftp.UnitTests/Fakes/FtpAsciiOutput.cs` (new) and seven existing test files whose ASCII/listing output assertions pinned CRLF, which would turn red on Linux and macOS CI once the conversion lands (found by running the suite with the conversion forced on). No task in Doing on origin/work/dark-factory names any Ftp file.
- Fix: `FtpDownloadLineEndings` ports curl's `ftp_cw_lc_write` (CRLF and lone CR to LF, a CR at the end of one read held to the next byte or the end); `FtpSession.DownloadAsync` makes one per download, converting only for ASCII (listing, `-B`, `;type=a`) off Windows. Decision recorded in ADR-0818. Byte counts stay raw, as curl counts before its content writers.
- Measured (Record-CurlExchange.ps1 -Ftp -ListenAddress 172.26.96.1 -Curl wsl.exe; REF-LINUX curl 8.18.0 OpenSSL; PORT = Curl.Console published linux-x64 and run in WSL), `curl -sS <url>`, all exit 0, empty stderr, identical command transcripts:
  - `ftp://H:20986/dir/`, LIST data `drw d.txt\r\n-rw e.txt\r\n`: REF `drw d.txt\n-rw e.txt\n`; PORT before (report) kept CRLF, PORT now `drw d.txt\n-rw e.txt\n`.
  - `'ftp://H:20986/crlf.txt;type=a'`, data `a\r\nb\r\nc\r\n`: REF and PORT `a\nb\nc\n`.
  - `ftp://H:20986/dir/`, LIST data `a\rb\r\n` (v_lonecr): REF and PORT `a\nb\n`.
  - `-l ftp://H:20986/dir/` (NLST), `a\r\nb\r\n`: REF and PORT `a\nb\n`.
  - `-B ftp://H:20986/f.txt`, `a\r\nb\rc\r`: REF and PORT `a\nb\nc\n`.
  - Binary `ftp://H:20986/f.txt`, `a\r\nb\r\n` (v_bin, already matched): REF and PORT `a\r\nb\r\n`.
- Windows (already matched, still matches): curl 8.22.0 Schannel and Curl.Console both write `drw d.txt\r\n-rw e.txt\r\n` for the listing and `a\r\nb\rc\r` for `;type=a`, exit 0, empty stderr.
- Focused test: `Cgr024Tests` (converter cases from every repro, across reads, passthrough on Windows/binary; a Linux/macOS-only session test and a Windows-only one). The converter type did not exist before the fix, so the tests could not pass before it.
- Gates: dotnet build 0 warnings 0 errors; fast tests 46 projects passed, 0 failed; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary`: 0 failing members; `dotnet format --verify-no-changes` clean on the test project. Linux/macOS CI is met by keeping the tests platform-neutral (`FtpAsciiOutput.Of`, OSCondition on the session tests).

## Log

- 2026-10-26: Created from CGR-024 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. On Linux and macOS an ASCII FTP download (LIST, NLST, -B, ;type=a) writes CRLF and lone CR as LF, as curl does; Windows and binary downloads unchanged
