---
id: BL-3629
title: Fix CGR-100: -P ::1 on an IPv4 control connection sends EPRT |2|::1|; curl falls back to the control connection's IPv4 address
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Protocol.Ftp.UnitTests/Cgr100Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3629 — Fix CGR-100: -P ::1 on an IPv4 control connection sends EPRT |2|::1|; curl falls back to the control connection's IPv4 address

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-100 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-100: `-P ::1` on an IPv4 control connection sends `EPRT |2|::1|`; curl falls back to the control connection's IPv4 address

- **Severity:** Medium
- **Area:** ftp-file-tftp
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `ftp-file-tftp FTP-7`

**Reproduction**

- *Setup (ftp-file-tftp):* EV = `SCR\evidence\ftp-file-tftp`. Scripted FTP/TFTP stub: `EV\stub\win3\stub.exe` (Linux `EV/stub/linux/stub`, source `EV\stub\Program.cs`) with fixture root `EV\fixture\root` (file.txt is 18 bytes). Differential harness `EV\dt.sh NAME 'script' curl-args`, where `@U@` = `ftp://127.0.0.1:$CPORT`, script lines are `CMD=reply` or `OPT key=value` separated by `;;`, and `STUBEXTRA=EV\tls.pfx` enables FTPS. Verifier: `EV\v1.sh`, `EV\v2.sh` (Windows), `EV\vl.sh` (Linux).
- `curl -sS -P ::1 ftp://127.0.0.1:20964/file.txt`. Case: `v_P_ipv6` (`EV\v2.sh`, TMO=120). Linux `cases-linux\m_P_ipv6`: REF exit 0, PORT killed at the harness timeout.

Commands (run each with REF and with PORT):

```sh
curl -sS -P ::1 ftp://127.0.0.1:20964/file.txt
EV\v2.sh
```

**Expected (real curl)**

- Sends `EPRT |1|127.0.0.1|<port>|`. The transfer succeeds with exit 0 and the file printed.

**Actual (Curl)**

- Sends `EPRT |2|::1|<port>|`. The server cannot connect, and after 60 s PORT exits 12 with `Accept timeout occurred while waiting server connect`.

**Suspected location in source**

- `Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs:262` `announcedAddress`, set at `:1634` and used at `:1528`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr100Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-100 of the third-party gap report.
