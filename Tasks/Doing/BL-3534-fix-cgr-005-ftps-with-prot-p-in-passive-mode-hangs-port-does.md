---
id: BL-3534
title: Fix CGR-005: FTPS with PROT P in passive mode hangs: PORT does the data-connection TLS handshake before sending TYPE/SIZE/RETR, deadlocking with serve...
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Protocol.Ftp.UnitTests/Cgr005Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3534 — Fix CGR-005: FTPS with PROT P in passive mode hangs: PORT does the data-connection TLS handshake before sending TYPE/SIZE/RETR, deadlocking with serve...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-005 (severity Critical): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-005: FTPS with PROT P in passive mode hangs: PORT does the data-connection TLS handshake before sending TYPE/SIZE/RETR, deadlocking with servers (vsftpd/proftpd ordering) that start data TLS after the transfer command

- **Severity:** Critical
- **Area:** ftp-file-tftp, tls
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: [ftp-file-tftp FTP-1] Confirmed (reproduced by the original tester and again here, on Windows and Linux); [tls TLS-01] Confirmed (verifier reproduced it on Windows and on Linux)
- **Related existing finding (GF/AF):** GF-0048
- **Source findings:** `ftp-file-tftp FTP-1`, `tls TLS-01`

**Reproduction**

- *Setup (ftp-file-tftp):* EV = `SCR\evidence\ftp-file-tftp`. Scripted FTP/TFTP stub: `EV\stub\win3\stub.exe` (Linux `EV/stub/linux/stub`, source `EV\stub\Program.cs`) with fixture root `EV\fixture\root` (file.txt is 18 bytes). Differential harness `EV\dt.sh NAME 'script' curl-args`, where `@U@` = `ftp://127.0.0.1:$CPORT`, script lines are `CMD=reply` or `OPT key=value` separated by `;;`, and `STUBEXTRA=EV\tls.pfx` enables FTPS. Verifier: `EV\v1.sh`, `EV\v2.sh` (Windows), `EV\vl.sh` (Linux).
- *Setup (tls):* E = `SCR\evidence\tls`. PKI in `E\pki` (`index.txt` contains `hello tls`; pfx password `x`). Servers: `openssl s_server -accept <p> -cert E\pki\srv.crt -key E\pki\srv.key -WWW [options]` (Git ucrt64 openssl on Windows, system openssl in WSL, run from a directory containing `index.txt`), `E\srv\out\starttls.exe <proto> <port> <pfx> [implicit|refuse|noext]` (source `E\srv\starttls.cs`), `E\linux\stsrv.py` (same idea in Python). Harnesses `E\tlsdiff.ps1`, `E\linux\tlsdiff.sh`; verifier `E\verify\vwin1.ps1`, `vwin2.ps1`, `vlin.sh` (cases `E\cases\V_*`, `E\linux\cases\V_L_*`).
- **[ftp-file-tftp FTP-1]** FTPS with PROT P (`--ssl-reqd`, `--ssl`, `ftps://`) hangs forever in passive mode when the server starts data-channel TLS only after the transfer command (vsftpd/proftpd ordering)
  - Stub with TLS, using the default lazy data-TLS mode: `stub.exe ftp 20960 <root> <script> <log> EV\tls.pfx` (in the harness, `STUBEXTRA=EV\tls.pfx`). Run `curl -sS -k --ssl-reqd ftp://127.0.0.1:20960/file.txt`. Implicit FTPS uses script `OPT implicit=1` with `curl -sS -k ftps://127.0.0.1:20960/file.txt`. Cases: `v_s_reqd` and `v_s_implicit` (Windows, `EV\v1.sh`); `v_s_reqd` (Linux, `EV\vl.sh`). The original cases are under `EV\cases\s_*`.
- **[tls TLS-01]** FTPS with PROT P (passive mode) hangs. PORT does the data-connection TLS handshake before it sends TYPE/SIZE/RETR
  - *(Windows):* `E\srv\out\starttls.exe ftp 20800 E\pki\srv.pfx`. The server does the data-channel TLS accept only after RETR/LIST, as vsftpd does. Then run `curl -sS -k --ssl-reqd ftp://localhost:20800/file.txt`. The same happens with `--ssl`, `--ftp-ssl`, `--ftp-ssl-reqd`, `--ftp-ssl-control`, `--ftp-ssl-ccc`, a directory LIST, and implicit `ftps://` (server flag `implicit`). Script: `E\verify\vwin1.ps1` (cases `V_ftp_sslreqd`, `V_ftps_implicit`); the original run is `E\batch5.ps1`.
  - *(Linux):* `python3 E/linux/stsrv.py ftp <port> <pkidir>`, then the same command (`E\verify\vlin.sh`, cases `V_L_ftp_sslreqd`, `V_L_ftps_implicit`).

Commands (run each with REF and with PORT):

```sh
# [ftp-file-tftp FTP-1]
stub.exe ftp 20960 <root> <script> <log> EV\tls.pfx
export STUBEXTRA=EV\tls.pfx
curl -sS -k --ssl-reqd ftp://127.0.0.1:20960/file.txt
curl -sS -k ftps://127.0.0.1:20960/file.txt
EV\v1.sh
EV\vl.sh
# [tls TLS-01]
E\srv\out\starttls.exe ftp 20800 E\pki\srv.pfx
curl -sS -k --ssl-reqd ftp://localhost:20800/file.txt
E\verify\vwin1.ps1
E\batch5.ps1
python3 E/linux/stsrv.py ftp <port> <pkidir>
E\verify\vlin.sh
```

**Expected (real curl)**

- **[ftp-file-tftp FTP-1]** Connects the data socket and keeps the TLS handshake pending. It sends TYPE I, SIZE and RETR on the control connection, and the handshake completes after RETR. Exit 0, the file is delivered.
- **[tls TLS-01]** After `229 EPSV` it sends `TYPE I`, `SIZE file.txt`, `RETR file.txt`. The server then accepts TLS on the data connection and REF prints `hello ftp`. Exit 0 on Linux. On Windows the exit is 0 or `56 Recv failure: Connection was aborted`, caused by the stand-in server's close.

**Actual (Curl)**

- **[ftp-file-tftp FTP-1]** After the 229 reply it connects the data socket and blocks in the data TLS handshake. It never sends TYPE I, so client and server deadlock. The run was killed by the timeout wrapper: exit 124 at 30 s in this run, and still hung at 200 s in the original run. Wire diff: REF sends `TYPE I`, `SIZE file.txt`, `RETR file.txt` and `QUIT`; PORT sends nothing after `EPSV`.
  - Control: with script `OPT eagertls=1`, where the server starts the handshake at accept time, PORT matches REF (`v_se_reqd` is SAME on both OSes). Active mode (`-P`) is not affected.
- **[tls TLS-01]** The server log stops at `EPSV`. PORT has connected the data socket and blocks in the TLS handshake, so the two sides deadlock. There is no output until the process is killed (20 s harness timeout; Linux exit 124). With `-m 5` it exits 28 `Operation timed out after 5007 milliseconds with 0 bytes received`. A plain `ftp://` transfer without TLS matches REF.

**Suspected location in source**

- **[ftp-file-tftp FTP-1]** `Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs:1788-1808` `OpenPassiveDataConnectionAsync` calls `SecureDataConnectionAsync` (`:1757`) right after the passive connect, before the transfer command. curl only adds the SSL filter there and completes the handshake without blocking after RETR/STOR/LIST.
- **[tls TLS-01]** `Curl.Protocol.Ftp.UnitLibrary\FtpSession.cs:1808`. On the EPSV path, `SecureDataConnectionAsync()` is awaited right after the dial. The same happens in `ConnectDataAsync` at about line 1900, where a comment says the handshake runs "at once, before TYPE". `SecureDataConnectionAsync` (line 1757) awaits the whole handshake.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr005Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-005 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
