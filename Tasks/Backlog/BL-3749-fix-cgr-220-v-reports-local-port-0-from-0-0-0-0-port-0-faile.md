---
id: BL-3749
title: Fix CGR-220: -v reports local port 0 (from 0.0.0.0 port 0 failed) for a failed connect, and prints the line where Windows REF prints none when moving...
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitTests/Cgr220Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3749 — Fix CGR-220: -v reports local port 0 (from 0.0.0.0 port 0 failed) for a failed connect, and prints the line where Windows REF prints none when moving...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-220 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-220: `-v` reports local port 0 (`from 0.0.0.0 port 0 failed`) for a failed connect, and prints the line where Windows REF prints none when moving to the next address

- **Severity:** Low
- **Area:** writeout-verbose, cli-options, proxy-connect, doh-ipfs-resolution
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `writeout-verbose WV-17`, `cli-options CLIO-19`, `proxy-connect PC-15`, `doh-ipfs-resolution DOH-17`

**Reproduction**

- *Setup (writeout-verbose):* EV = `SCR\evidence\writeout-verbose`. Windows server: `cd EV\srv && dotnet run wvsrv.cs -- 20700 EV\resp EV\req.log` (HTTP on 127.0.0.1/::1:20700, a mini FTP server on 20701, TLS on 20702; `/raw/<name>` serves `EV\resp\<name>.txt`; other routes `/code/N`, `/redir/N`, `/hang/ms`, `/close` (empty reply), `/ka`, `/echo`, `/p`). Linux: `EV\linux\srv.py` on 20710. One-line repros for every finding: `EV\repro_findings.sh` (Windows) and `EV\linux\verify_linux.sh`.
- *Setup (cli-options):* Evidence `SCR\evidence\cli-options`. Windows: start the echo server `pwsh -NoProfile -File SCR\evidence\cli-options\echoserver.ps1 -Port 20000` (replies 200 with the raw request bytes as the body); `@U` = `http://127.0.0.1:20000/`; port 20002 is left closed. Harness: `SCR\evidence\cli-options\h.sh <name> <args>` runs REF and PORT and stores exit code, stdout, stderr and files. Linux: `python3 linux/echo.py 20001` (in the evidence folder); `URL` = `http://127.0.0.1:20001/`. Verifier scripts: `verify/v1_win.sh`, `verify/v4_win.sh`, `verify/v2_linux.sh`, `verify/v3_linux.sh`.
- *Setup (proxy-connect):* EV = `SCR\evidence\proxy-connect`. Scripted recording server: `EV\srv\out\srv.exe <port> <log> '<script for conn 1>' ['<script for conn 2>' ...]` (Linux `EV/srv/outlinux/srv`); steps are `;`-separated: `H` read a request head, `HN` read a head and answer nothing yet, `OK`/`CE`/`403`/`403B`/`407B` canned replies, `W:<bytes>` write literal bytes, `S4`/`S5G`/`S5U`/`S5R` read SOCKS messages, `E` record raw bytes until EOF. In repros `srv` means this binary. Harnesses `EV\harness.ps1` and `EV\lharness.sh`; verifier `EV\v_win*.ps1`, `EV\v_linux.sh`, `EV\v_l3.sh`.
- *Setup (doh-ipfs-resolution):* EV = `SCR\evidence\doh-ipfs-resolution`. Test server `EV\srv\out\srv.exe` (Linux `EV/srv/outlinux/srv`, source `EV\srv\Program.cs`); in repros `srv` means this binary. Options: `doh=<port>` plain-HTTP DoH responder, `dohs=<port>` TLS DoH (`selfsigned=1` or `pfx=`), `web=`/`webs=` web targets, `web2=` recording proxy, `rr=name:TYPE:addr` records, `mangle=http10|hang` faults, `webstatus=`/`weblocation=`. Harness `EV\harness.ps1`; verifier `EV\v1.ps1`, `EV\v2.ps1` (outputs `EV\cases\v_*.txt`); Linux `EV\linux\run1.sh vl.py`.
- **[writeout-verbose WV-17]** The connect-failure verbose line says "port 0" instead of the bound local port
  - `curl -s -v http://127.0.0.1:20799/` (nothing is listening).
- **[cli-options CLIO-19]** Verbose output for a failed connect reports local port 0
  - `curl -v http://127.0.0.1:20002/`.
- **[proxy-connect PC-15]** -v "connect to ... from 0.0.0.0 port 0 failed" shows port 0 instead of the local port that was used
  - with no server on the port, run `curl -sv http://127.0.0.1:20699/`. Verifier cases: v22 and VL17.
- **[doh-ipfs-resolution DOH-17]** -v prints "connect to ... from 0.0.0.0 port 0 failed" when moving to the next address
  - `srv web=21802`, then `curl -v -sS --resolve foo.test:21802:127.0.0.9,127.0.0.1 http://foo.test:21802/x`. Cases: `cases\v_f17_multi_resolve_v.txt`, `linux\cases\vl_f17_multi.txt`.

Commands (run each with REF and with PORT):

```sh
# [writeout-verbose WV-17]
curl -s -v http://127.0.0.1:20799/
# [cli-options CLIO-19]
curl -v http://127.0.0.1:20002/
# [proxy-connect PC-15]
curl -sv http://127.0.0.1:20699/
# [doh-ipfs-resolution DOH-17]
srv web=21802
curl -v -sS --resolve foo.test:21802:127.0.0.9,127.0.0.1 http://foo.test:21802/x
```

**Expected (real curl)**

- **[writeout-verbose WV-17]** 
  - Windows: `* connect to 127.0.0.1 port 20799 from 0.0.0.0 port 65138 failed: Connection refused`
  - Linux: `... from 127.0.0.1 port NNNNN ...`
- **[cli-options CLIO-19]** `* connect to 127.0.0.1 port 20002 from 0.0.0.0 port <ephemeral> failed: Connection refused`.
- **[proxy-connect PC-15]** `* connect to 127.0.0.1 port 20699 from 0.0.0.0 port 54415 failed: Connection refused`. Linux 8.18 prints `from 127.0.0.1 port N`.
- **[doh-ipfs-resolution DOH-17]** on Windows there is no `connect to ... failed` line between the two `Trying` lines. On Linux it prints `connect to 127.0.0.9 port 21802 from 127.0.0.1 port <n> failed: Connection refused`.

**Actual (Curl)**

- **[writeout-verbose WV-17]** `... from 0.0.0.0 port 0 failed: Connection refused`
- **[cli-options CLIO-19]** `... from 0.0.0.0 port 0 failed: Connection refused`.
- **[proxy-connect PC-15]** `... from 0.0.0.0 port 0 failed: Connection refused` on both platforms.
- **[doh-ipfs-resolution DOH-17]** `* connect to 127.0.0.9 port 21802 from 0.0.0.0 port 0 failed: Connection refused` on both platforms.

**Suspected location in source**

- **[writeout-verbose WV-17]** `Curl.Networking.UnitLibrary/TcpConnector.cs` does not read the local endpoint after a failed connect.
- **[doh-ipfs-resolution DOH-17]** `Curl.Networking.UnitLibrary/TcpConnector.cs`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr220Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-220 of the third-party gap report.
