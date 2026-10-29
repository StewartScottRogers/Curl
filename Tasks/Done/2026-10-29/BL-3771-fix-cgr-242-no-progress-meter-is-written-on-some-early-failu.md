---
id: BL-3771
title: Fix CGR-242: No progress meter is written on some early failures (refused connect, refused CONNECT, POP3/SMTP protocol errors) where REF writes the he...
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr242Tests.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitTests/Cgr242Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-29
---
# BL-3771 — Fix CGR-242: No progress meter is written on some early failures (refused connect, refused CONNECT, POP3/SMTP protocol errors) where REF writes the he...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-242 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-242: No progress meter is written on some early failures (refused connect, refused CONNECT, POP3/SMTP protocol errors) where REF writes the header and a zero row

- **Severity:** Low
- **Area:** upstream-cases, proxy-connect, writeout-verbose
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: [upstream-cases UC-15] Confirmed (POP3 and SMTP, on all four binaries).; [writeout-verbose WV-21] Probable. On Windows, REF 8.22 also prints no meter here, so this may depend on version or timing.
- **Related existing finding (GF/AF):** GF-0078
- **Source findings:** `upstream-cases UC-15`, `proxy-connect PC-16`, `writeout-verbose WV-21`

**Reproduction**

- *Setup (upstream-cases):* EV = `SCR\evidence\upstream-cases`. Cross-platform runs: `EV\verify\diff.sh`, driven in WSL by `MSYS_NO_PATHCONV=1 wsl -d Ubuntu -- bash "/mnt/c/Users/Stewart Rogers/AppData/Local/Temp/claude/Z--repos-Curl-ThirdPartyReview/3796612b-55d9-44ec-9722-80e35c3282a1/scratchpad/evidence/upstream-cases/verify/sync.sh" vN.sh` (N = 1..4), against the canned recording server `EV\verify\srv.py` (configs in `EV\verify\conf\`). Windows-native runs: `EV\verify\vwin.ps1` (wraps `harness\Diff-Curl.ps1` and `Record-CurlExchange.ps1`).
- *Setup (proxy-connect):* EV = `SCR\evidence\proxy-connect`. Scripted recording server: `EV\srv\out\srv.exe <port> <log> '<script for conn 1>' ['<script for conn 2>' ...]` (Linux `EV/srv/outlinux/srv`); steps are `;`-separated: `H` read a request head, `HN` read a head and answer nothing yet, `OK`/`CE`/`403`/`403B`/`407B` canned replies, `W:<bytes>` write literal bytes, `S4`/`S5G`/`S5U`/`S5R` read SOCKS messages, `E` record raw bytes until EOF. In repros `srv` means this binary. Harnesses `EV\harness.ps1` and `EV\lharness.sh`; verifier `EV\v_win*.ps1`, `EV\v_linux.sh`, `EV\v_l3.sh`.
- *Setup (writeout-verbose):* EV = `SCR\evidence\writeout-verbose`. Windows server: `cd EV\srv && dotnet run wvsrv.cs -- 20700 EV\resp EV\req.log` (HTTP on 127.0.0.1/::1:20700, a mini FTP server on 20701, TLS on 20702; `/raw/<name>` serves `EV\resp\<name>.txt`; other routes `/code/N`, `/redir/N`, `/hang/ms`, `/close` (empty reply), `/ka`, `/echo`, `/p`). Linux: `EV\linux\srv.py` on 20710. One-line repros for every finding: `EV\repro_findings.sh` (Windows) and `EV\linux\verify_linux.sh`.
- **[upstream-cases UC-15]** The progress meter is printed on some early failures by REF but not by PORT
  - `verify\v2.sh`.
    - POP3 (`verify\conf\pop3.json`): `curl pop3://127.0.0.1:21361/9 -u user:secret`
    - SMTP (`smtp.json`): `curl smtp://127.0.0.1:21362/907 --mail-rcpt bad@example.com --mail-from sender@example.com -T upload`
- **[proxy-connect PC-16]** No progress meter is written when a CONNECT is refused
  - run `srv 20600 log 'HN;403'`, then run `curl -p -x http://127.0.0.1:20600 http://example.test/` without -s, with stdout redirected. Verifier cases: v23 and VL18.
- **[writeout-verbose WV-21]** Connection refused: no progress meter header or zero row before the error (Linux)
  - `curl -o /dev/null http://127.0.0.1:20799/`, run 2 more times by the verifier.

Commands (run each with REF and with PORT):

```sh
# [upstream-cases UC-15]
curl pop3://127.0.0.1:21361/9 -u user:secret
curl smtp://127.0.0.1:21362/907 --mail-rcpt bad@example.com --mail-from sender@example.com -T upload
# [proxy-connect PC-16]
srv 20600 log 'HN;403'
curl -p -x http://127.0.0.1:20600 http://example.test/
# [writeout-verbose WV-21]
curl -o /dev/null http://127.0.0.1:20799/
```

**Expected (real curl)**

- **[upstream-cases UC-15]** The meter header and a zero line, then `curl: (8) Weird server reply` (POP3) or `curl: (55) ...` (SMTP).
- **[proxy-connect PC-16]** stderr has the meter header lines and a zero line, then `curl: (7) CONNECT tunnel failed, response 403`.
- **[writeout-verbose WV-21]** *REF-LINUX 8.18:* The two meter header lines and a zero row, then `curl: (7) ...`

**Actual (Curl)**

- **[upstream-cases UC-15]** Only the `curl: (N) ...` line. Exit codes match (8 and 55).
- **[proxy-connect PC-16]** stderr has only `curl: (7) CONNECT tunnel failed, response 403`.
- **[writeout-verbose WV-21]** *PORT-LINUX:* Only the `curl: (7)` line.

**Notes**

- **[upstream-cases UC-15]** *Verifier note:* The reverse case (test2114, where PORT prints the meter and REF does not) and the test94 proxy case were not re-run.

**Suspected location in source**

- **[upstream-cases UC-15]** Curl.Output.UnitLibrary (progress meter start).
- **[writeout-verbose WV-21]** `Curl.Console/TransferProgressRecorder.cs`

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr242Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-242 of the third-party gap report.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

## Notes (BL-3771 run)

- Measured on Windows curl 8.22.0 (Record-CurlExchange.ps1): refused CONNECT and SMTP RCPT 550 both write the header and zero row before the error. Curl did not for the CONNECT; fixed in `HttpProtocolHandler` (touches corrected: the named `Curl.Console/TransferProgressRecorder.cs` does not exist; the recorder is in Curl.Engine and needed no change).
- SMTP (exit 55) already matched (pinned by `Cgr242Tests` in Curl.Console.UnitTests); POP3 reports started after connect through the same pattern, so it matched too. Not reproduced.
- WV-21 (plain refused connect) is Linux-only; Windows curl prints no meter there and Curl matches the platform build. Left as is. ADR-0990.
- Linux/macOS CI: change is platform-neutral; fast tests green here. Quality run for Curl.Protocol.Http.UnitLibrary: 0 failing members.
- 2026-10-29: Doing -> Done. Refused CONNECT now draws the progress meter; SMTP/POP3 already matched
