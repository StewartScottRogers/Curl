---
id: BL-3760
title: Fix CGR-231: Wrong exit-28 message when the 226 reply never arrives
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Protocol.Ftp.UnitLibrary/FtpTransferMessages.cs, Curl.Protocol.Ftp.UnitTests/Cgr231Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3760 — Fix CGR-231: Wrong exit-28 message when the 226 reply never arrives

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-231 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-231: Wrong exit-28 message when the 226 reply never arrives

- **Severity:** Low
- **Area:** ftp-file-tftp
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `ftp-file-tftp FTP-11`

**Reproduction**

- *Setup (ftp-file-tftp):* EV = `SCR\evidence\ftp-file-tftp`. Scripted FTP/TFTP stub: `EV\stub\win3\stub.exe` (Linux `EV/stub/linux/stub`, source `EV\stub\Program.cs`) with fixture root `EV\fixture\root` (file.txt is 18 bytes). Differential harness `EV\dt.sh NAME 'script' curl-args`, where `@U@` = `ftp://127.0.0.1:$CPORT`, script lines are `CMD=reply` or `OPT key=value` separated by `;;`, and `STUBEXTRA=EV\tls.pfx` enables FTPS. Verifier: `EV\v1.sh`, `EV\v2.sh` (Windows), `EV\vl.sh` (Linux).
- Script `RETR.226=NONE`. Run `curl -sS -m 4 ftp://127.0.0.1:20960/file.txt`, and also without `-m`, which waits 60 s. Cases: `v_no226` (Windows and Linux); original `EV\cases\no_226_nom`.

Commands (run each with REF and with PORT):

```sh
curl -sS -m 4 ftp://127.0.0.1:20960/file.txt
```

**Expected (real curl)**

- `curl: (28) FTP response timeout`.

**Actual (Curl)**

- With `-m 4`: `curl: (28) Operation timed out after 4006 milliseconds with 18 out of 18 bytes received`. Without `-m`: `curl: (28) server response timeout`. The exit code and stdout match.

**Suspected location in source**

- `Curl.Protocol.Ftp.UnitLibrary/FtpTransferMessages.cs:36` and `:51`; `FtpSession.cs:2682-2725`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr231Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Fix: the end-of-transfer reply wait (FtpSession.ReadTransferCompleteAsync) now fails exit 28 with "FTP response timeout" when the server response timeout passes (was "server response timeout") and when the -m limit cancels the wait (was Core's "Operation timed out after ... bytes received"). Not re-measured against live curl here; text taken from the gap report (REF). Pinned by Cgr231Tests (both paths); other repro variants untouched. Linux/macOS: platform-neutral tests.

## Log

- 2026-10-26: Created from CGR-231 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
