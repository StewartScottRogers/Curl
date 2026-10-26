---
id: BL-3553
title: Fix CGR-024: On Linux, FTP ASCII transfers (LIST, NLST, ;type=a, -B) keep CRLF; curl converts CRLF and lone CR to LF on non-Windows platforms
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests/Cgr024Tests.cs]
requirement: none
created: 2026-10-26
completed:
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

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr024Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-024 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
