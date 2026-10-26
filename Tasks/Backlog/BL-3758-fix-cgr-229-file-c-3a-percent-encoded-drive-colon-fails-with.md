---
id: BL-3758
title: Fix CGR-229: file:///C%3A/... (percent-encoded drive colon) fails with exit 37
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.File.UnitLibrary/FileUrlPath.cs, Curl.Protocol.File.UnitTests/Cgr229Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3758 — Fix CGR-229: file:///C%3A/... (percent-encoded drive colon) fails with exit 37

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-229 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-229: `file:///C%3A/...` (percent-encoded drive colon) fails with exit 37

- **Severity:** Low
- **Area:** ftp-file-tftp
- **Platform(s):** Windows
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** GF-0041
- **Source findings:** `ftp-file-tftp FILE-2`

**Reproduction**

- *Setup (ftp-file-tftp):* EV = `SCR\evidence\ftp-file-tftp`. Scripted FTP/TFTP stub: `EV\stub\win3\stub.exe` (Linux `EV/stub/linux/stub`, source `EV\stub\Program.cs`) with fixture root `EV\fixture\root` (file.txt is 18 bytes). Differential harness `EV\dt.sh NAME 'script' curl-args`, where `@U@` = `ftp://127.0.0.1:$CPORT`, script lines are `CMD=reply` or `OPT key=value` separated by `;;`, and `STUBEXTRA=EV\tls.pfx` enables FTPS. Verifier: `EV\v1.sh`, `EV\v2.sh` (Windows), `EV\vl.sh` (Linux).
- `curl -sS file:///C%3A/Windows/win.ini`. Case: `v_fw_enc`; originals `EV\cases\fw_enc_colon*`.

Commands (run each with REF and with PORT):

```sh
curl -sS file:///C%3A/Windows/win.ini
```

**Expected (real curl)**

- Prints win.ini, exit 0.

**Actual (Curl)**

- `curl: (37) Could not open file /C%3A/Windows/win.ini`.

**Suspected location in source**

- `Curl.Protocol.File.UnitLibrary/FileUrlPath.cs:174` and `:189-195`. `StripDriveLetterSlash` checks the still-encoded path for `:` or `|`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr229Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-229 of the third-party gap report.
