---
id: BL-3629
title: Fix CGR-100: -P ::1 on an IPv4 control connection sends EPRT |2|::1|; curl falls back to the control connection's IPv4 address
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Protocol.Ftp.UnitLibrary/FtpPortArgument.cs, Curl.Protocol.Ftp.UnitTests/Cgr100Tests.cs, Curl.Protocol.Ftp.UnitTests/FtpProtocolHandlerActiveModeTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
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

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr100Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Cause: real curl does not resolve or bind `::1` here. It reads a `-P` value that starts with a colon as having no address, so it uses the control connection's own address, and it reads what follows the first colon as the port (`:1` is any port). `FtpPortArgument.SplitAddress` read the whole value as a bare IPv6 literal. Fixed there: a leading colon now means an empty address. `FtpSession.cs` needed no change.
- Touches widened to `FtpPortArgument.cs`, where the fix lands, and to `FtpProtocolHandlerActiveModeTests.cs`, whose `("::1", "::1")` row pinned the old behaviour. That row is now `("::1", "127.0.0.1")`, with a new `("0::1", "::1")` row beside it. No task in Doing on origin/work/dark-factory names either file.
- Measured with Record-CurlExchange.ps1 -Ftp against curl 8.21.0 Schannel (the local `curl`), with the control connection on 127.0.0.1, `-sS -P <value> ftp://127.0.0.1:<port>/file.txt`, file of 18 bytes. REF and Curl (`dotnet run --project Curl.Console`) after the fix:
  - `::1`: REF `EPRT |1|127.0.0.1|<port>|`, exit 0, 18 bytes, no stderr; Curl the same. Transcripts are identical once the port is normalised.
  - `::1:5000`: both `EPRT |1|127.0.0.1|<any port>|`, exit 0.
  - `0::1` and `[::1]`: both `EPRT |2|::1|<port>|`, exit 0. These already matched and still do.
  - `::ffff:127.0.0.1` and `localhost` (`EPRT |2|::1|`, ADR-0108): both match, exit 0.
- Not measured on Linux in this run. The fix is pure string parsing, so it is platform-neutral, and the report says the Linux REF also succeeds. Fast tests are green here.
- No ADR: this matches measured curl behaviour and involved no choice.
- Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary: 0 failing members.

## Log

- 2026-10-26: Created from CGR-100 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. -P ::1 (any -P value starting with a colon) announces the control connection's address, as curl does
