---
id: BL-3630
title: Fix CGR-101: --connect-timeout is not applied to the FTP passive data connection on Linux, so PORT hangs where curl exits 28 after 2 s
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Protocol.Ftp.UnitTests/Cgr101Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3630 — Fix CGR-101: --connect-timeout is not applied to the FTP passive data connection on Linux, so PORT hangs where curl exits 28 after 2 s

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-101 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-101: `--connect-timeout` is not applied to the FTP passive data connection on Linux, so PORT hangs where curl exits 28 after 2 s

- **Severity:** Medium
- **Area:** ftp-file-tftp
- **Platform(s):** Linux
- **Confidence:** Probable. Detail: Probable. It reproduces, but only REF-LINUX 8.18 is available on Linux, and on Windows REF 8.22 and PORT behave the same (both exit 28 after about 21 s).
- **Related existing finding (GF/AF):** GF-0054
- **Source findings:** `ftp-file-tftp FTP-10`

**Reproduction**

- *Setup (ftp-file-tftp):* EV = `SCR\evidence\ftp-file-tftp`. Scripted FTP/TFTP stub: `EV\stub\win3\stub.exe` (Linux `EV/stub/linux/stub`, source `EV\stub\Program.cs`) with fixture root `EV\fixture\root` (file.txt is 18 bytes). Differential harness `EV\dt.sh NAME 'script' curl-args`, where `@U@` = `ftp://127.0.0.1:$CPORT`, script lines are `CMD=reply` or `OPT key=value` separated by `;;`, and `STUBEXTRA=EV\tls.pfx` enables FTPS. Verifier: `EV\v1.sh`, `EV\v2.sh` (Windows), `EV\vl.sh` (Linux).
- WSL. Script `OPT pasvip=10,9,8,7`, then `curl -sS --disable-epsv --no-ftp-skip-pasv-ip --connect-timeout 2 -w '%{time_total}\n' ftp://127.0.0.1:20986/file.txt`. Case: `v_pasv_ct` (`EV\vl.sh`, TMO=40); original `cases-linux\pasv_unreach_ct` (TMO=100).

Commands (run each with REF and with PORT):

```sh
curl -sS --disable-epsv --no-ftp-skip-pasv-ip --connect-timeout 2 -w '%{time_total}\n' ftp://127.0.0.1:20986/file.txt
EV\vl.sh
```

**Expected (real curl)**

- *REF behaviour (REF-LINUX 8.18):* `curl: (28) Connection timeout after 2002 ms`, time_total 2.0.

**Actual (Curl)**

- No exit within 40 s (100 s in the original run). It was killed with exit 124.

**Suspected location in source**

- `Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs:1878-1898` `ConnectToPassiveAddressAsync` / `ConnectDataAsync`. Not narrowed.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr101Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-101 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
