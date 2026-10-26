---
id: BL-3617
title: Fix CGR-088: -v/--trace do not echo error messages as * <message> info lines (curl's failf echo) for HTTP, FTP, CONNECT and SOCKS failures
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitTests/Cgr088Tests.cs, Curl.Networking.UnitTests/TcpConnectorTests.ProxyAuthVerbose.cs, Curl.Networking.UnitTests/TcpConnectorTests.SocksFilterTrace.cs, Curl.Networking.UnitTests/TcpConnectorQuicTests.UdpTunnelHttpsConnectTrace.cs, Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Protocol.Ftp.UnitLibrary/FtpTransferMessages.cs, Curl.Protocol.Ftp.UnitTests/Cgr088Tests.cs, Curl.Protocol.Ftp.UnitTests/FtpProtocolHandlerActiveModeTests.cs, Curl.Protocol.Ftp.UnitTests/FtpProtocolHandlerDataConnectionEventTests.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitTests/Cgr088Tests.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.ConnectionReuse.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.HeadInfoLines.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3617 — Fix CGR-088: -v/--trace do not echo error messages as * <message> info lines (curl's failf echo) for HTTP, FTP, CONNECT and SOCKS failures

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-088 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-088: `-v`/`--trace` do not echo error messages as `* <message>` info lines (curl's failf echo) for HTTP, FTP, CONNECT and SOCKS failures

- **Severity:** Medium
- **Area:** writeout-verbose, ftp-file-tftp, proxy-connect
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: [writeout-verbose WV-06] Confirmed (Windows and Linux).
- **Related existing finding (GF/AF):** AF-0085
- **Source findings:** `writeout-verbose WV-06`, `ftp-file-tftp FTP-12`, `proxy-connect PC-11`

**Reproduction**

- *Setup (writeout-verbose):* EV = `SCR\evidence\writeout-verbose`. Windows server: `cd EV\srv && dotnet run wvsrv.cs -- 20700 EV\resp EV\req.log` (HTTP on 127.0.0.1/::1:20700, a mini FTP server on 20701, TLS on 20702; `/raw/<name>` serves `EV\resp\<name>.txt`; other routes `/code/N`, `/redir/N`, `/hang/ms`, `/close` (empty reply), `/ka`, `/echo`, `/p`). Linux: `EV\linux\srv.py` on 20710. One-line repros for every finding: `EV\repro_findings.sh` (Windows) and `EV\linux\verify_linux.sh`.
- *Setup (ftp-file-tftp):* EV = `SCR\evidence\ftp-file-tftp`. Scripted FTP/TFTP stub: `EV\stub\win3\stub.exe` (Linux `EV/stub/linux/stub`, source `EV\stub\Program.cs`) with fixture root `EV\fixture\root` (file.txt is 18 bytes). Differential harness `EV\dt.sh NAME 'script' curl-args`, where `@U@` = `ftp://127.0.0.1:$CPORT`, script lines are `CMD=reply` or `OPT key=value` separated by `;;`, and `STUBEXTRA=EV\tls.pfx` enables FTPS. Verifier: `EV\v1.sh`, `EV\v2.sh` (Windows), `EV\vl.sh` (Linux).
- *Setup (proxy-connect):* EV = `SCR\evidence\proxy-connect`. Scripted recording server: `EV\srv\out\srv.exe <port> <log> '<script for conn 1>' ['<script for conn 2>' ...]` (Linux `EV/srv/outlinux/srv`); steps are `;`-separated: `H` read a request head, `HN` read a head and answer nothing yet, `OK`/`CE`/`403`/`403B`/`407B` canned replies, `W:<bytes>` write literal bytes, `S4`/`S5G`/`S5U`/`S5R` read SOCKS messages, `E` record raw bytes until EOF. In repros `srv` means this binary. Harnesses `EV\harness.ps1` and `EV\lharness.sh`; verifier `EV\v_win*.ps1`, `EV\v_linux.sh`, `EV\v_l3.sh`.
- **[writeout-verbose WV-06]** -v does not echo error messages as "* <message>" info lines (curl's failf echo)
  - `curl -s -v -f http://127.0.0.1:20700/code/404`
  - `curl -s -v http://127.0.0.1:20700/close` (empty reply)
  - `curl -s -v -u bad:x ftp://127.0.0.1:20701/file.txt` (the server answers 530)
  - The same gap applies to `--trace` and to every failure path the original tester listed: `--max-redirs`, `--max-filesize`, bad gzip, bad chunk, `-m`, a bad `-o` path, a short body, FTP CWD/STOR/RETR/REST errors, and a bad SOCKS5 reply.
- **[ftp-file-tftp FTP-12]** `-v` FTP failures are not echoed as `* <message>` info lines, and `* shutting down / closing connection #0` is missing
  - `curl -sSv ftp://127.0.0.1:20960/nope.txt`
  - `curl -sSv -Q BOGUS ftp://127.0.0.1:20960/file.txt`
  - Other cases: `EV\cases\vf_*` (PASS 530, EPSV+PASV 500, --max-filesize, greeting 500, 226 replaced by 451).

  Verification cases: `v_vf_missing`, `v_vf_q` (Windows); `v_vf_missing` (Linux).
- **[proxy-connect PC-11]** -v does not print the failure as a "* " trace line for CONNECT and SOCKS failures
  - *(CONNECT):* run `srv 20600 log 'HN;403B'`, then `curl -sv -p -x http://127.0.0.1:20600 http://example.test/`.
  - *(SOCKS4):* run with script `S4;S4REJ`, then `curl -sv -x socks4://127.0.0.1:20600 http://127.0.0.1:8080/x`.

Commands (run each with REF and with PORT):

```sh
# [writeout-verbose WV-06]
curl -s -v -f http://127.0.0.1:20700/code/404
curl -s -v http://127.0.0.1:20700/close
curl -s -v -u bad:x ftp://127.0.0.1:20701/file.txt
# [ftp-file-tftp FTP-12]
curl -sSv ftp://127.0.0.1:20960/nope.txt
curl -sSv -Q BOGUS ftp://127.0.0.1:20960/file.txt
# [proxy-connect PC-11]
srv 20600 log 'HN;403B'
curl -sv -p -x http://127.0.0.1:20600 http://example.test/
curl -sv -x socks4://127.0.0.1:20600 http://127.0.0.1:8080/x
```

**Expected (real curl)**

- **[writeout-verbose WV-06]** Prints the error as an info line before ending, for example `* The requested URL returned error: 404`, `* Empty reply from server` followed by `* shutting down connection #0`, and `* Access denied: 530`. These come from libcurl `failf()`, which echoes to the verbose stream in release builds too. REF-LINUX 8.18 (a release build) prints them.
- **[ftp-file-tftp FTP-12]** `* The file does not exist`; `* QUOT command failed with 500` followed by `* shutting down connection #0`, and similar lines for the other failures.
- **[proxy-connect PC-11]** `* CONNECT tunnel failed, response 403` or `* [SOCKS] cannot complete SOCKS4 connection to 0.0.0.0:0. (91), request rejected or failed.` before `* closing connection #0`.

**Actual (Curl)**

- **[writeout-verbose WV-06]** None of these lines appear; only the final `curl: (N)` line is printed. For an empty reply it prints `* closing connection #0` where REF prints `* shutting down connection #0`.
- **[ftp-file-tftp FTP-12]** These lines are missing. The final `curl: (N) ...` line matches. file:// and TFTP verbose failures match.
- **[proxy-connect PC-11]** that line is missing.

**Notes**

- **[proxy-connect PC-11]** *Verifier cases:* v18, v18b and VL14.

**Suspected location in source**

- **[writeout-verbose WV-06]** There is no general failf-style verbose echo. `Curl.Console/CurlCommandRunner.cs` writes only the final line; only `SchannelFailureEcho.cs` exists, for TLS.
- **[ftp-file-tftp FTP-12]** `Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs` `QuitAndFailAsync` / `EndAndFailAsync` do not report the failure message as an info event.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr088Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- 2026-10-26 (second claim, lane 1): split for size as the first claim's log asked. This task now covers the issue's explicit repro commands; the "same gap applies" list (--max-redirs, --max-filesize, bad gzip, bad chunk, -m, bad -o path, short body, FTP STOR/REST/greeting/226-451, SOCKS5 replies, --trace) is BL-3872.
- The earlier claim measured real curl 8.22.0 (Schannel) through Record-CurlExchange.ps1 and kept the work in the stash this claim finished; the lines pinned, each the issue's Expected text: `-f` 404 -> `* The requested URL returned error: 404` before the head's empty line; empty reply -> `* Empty reply from server`, `* shutting down connection #0` (was `closing`); `-u bad:x` FTP 530 -> `* Access denied: 530`, `* closing connection #0`; FTP SIZE 550 -> `* The file does not exist` before the directory and left-intact lines (RETR 550 echoes `RETR response: 550`, curl's ftp.c failf text); `-Q BOGUS` 500 -> `* QUOT command failed with 500`, `* shutting down connection #0`; CONNECT 403 -> `* CONNECT tunnel failed, response 403`; SOCKS4 91 -> `* [SOCKS] cannot complete SOCKS4 connection to <replied address>. (91), request rejected or failed.`. Exit codes and final `curl: (N)` lines already matched and still do.
- Decision (sensible default): the echo is written at each failing site, not as a general echo in CurlCommandRunner, since many paths (TLS, SOCKS5 GSS-API, FTP PORT) already write their failf lines and a general echo would repeat them; for the same reason the SOCKS echo covers SOCKS4/4a only. CurlCommandRunner, SchannelFailureEcho and the Console test named in the original touches were not needed and are dropped from touches.
- Touches widened to TcpConnector.cs, HttpProtocolHandler.cs, FtpTransferMessages.cs and the existing tests pinning the old transcripts: no task in Doing on origin/work/dark-factory names them (checked: BL-3606, 3610, 3612, 3616, 3620, 3622, 3869).
- Focused tests (ADR-0482): Curl.Protocol.Ftp.UnitTests/Cgr088Tests.cs, Curl.Protocol.Http.UnitTests/Cgr088Tests.cs, Curl.Networking.UnitTests/Cgr088Tests.cs; each fails without the production change (the lines are absent).
- All changes are platform-neutral (event text only, no paths or OS errors), so CI on Linux and macOS follows the Windows run.
- Measure-CodeQuality.ps1 for Ftp, Http and Networking: two methods went to complexity 12 and were split (ClosesTheConnection, ReportSocks4Failure); the rerun for Http and Networking reports 0 failing members. Full fast suite green.
## Log

- 2026-10-26: Created from CGR-088 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Backlog. Dark factory stopped at its 80-turn cap, so the task is too big for one run: split it in the first turns of the next claim; see Z:\repos\Curl.logs\BL-3617-20261026-063535-L5.jsonl
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. -v echoes curl's failf line for -f 404, an empty reply, FTP 530/550/QUOT, CONNECT refusals and SOCKS4 rejections; the remaining failure paths are BL-3872
