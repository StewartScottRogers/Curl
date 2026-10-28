---
id: BL-3761
title: Fix CGR-232: -I -v on an FTP file omits * Remembering we are in directory "" and * Connection #0 to host ... left intact
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Protocol.Ftp.UnitTests/Cgr232Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3761 — Fix CGR-232: -I -v on an FTP file omits * Remembering we are in directory "" and * Connection #0 to host ... left intact

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-232 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-232: `-I -v` on an FTP file omits `* Remembering we are in directory ""` and `* Connection #0 to host ... left intact`

- **Severity:** Low
- **Area:** ftp-file-tftp
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `ftp-file-tftp FTP-13`

**Reproduction**

- *Setup (ftp-file-tftp):* EV = `SCR\evidence\ftp-file-tftp`. Scripted FTP/TFTP stub: `EV\stub\win3\stub.exe` (Linux `EV/stub/linux/stub`, source `EV\stub\Program.cs`) with fixture root `EV\fixture\root` (file.txt is 18 bytes). Differential harness `EV\dt.sh NAME 'script' curl-args`, where `@U@` = `ftp://127.0.0.1:$CPORT`, script lines are `CMD=reply` or `OPT key=value` separated by `;;`, and `STUBEXTRA=EV\tls.pfx` enables FTPS. Verifier: `EV\v1.sh`, `EV\v2.sh` (Windows), `EV\vl.sh` (Linux).
- `curl -sSv -I ftp://127.0.0.1:20960/file.txt`. Case: `v_dl_I_v` (Windows and Linux).

Commands (run each with REF and with PORT):

```sh
curl -sSv -I ftp://127.0.0.1:20960/file.txt
```

**Expected (real curl)**

- Prints both lines after `REST 0` / `350`.

**Actual (Curl)**

- Both lines are missing. The wire bytes and stdout match.

**Suspected location in source**

- `Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs`, the no-body path.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr232Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-232 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
