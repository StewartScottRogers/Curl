---
id: BL-3535
title: Fix CGR-006: Windows FTPS uploads end the TLS data connection with a TCP reset: in passive mode the server receives 0 bytes while PORT exits 0 (silent...
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Protocol.Ftp.UnitTests/Cgr006Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3535 — Fix CGR-006: Windows FTPS uploads end the TLS data connection with a TCP reset: in passive mode the server receives 0 bytes while PORT exits 0 (silent...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-006 (severity Critical): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-006: Windows FTPS uploads end the TLS data connection with a TCP reset: in passive mode the server receives 0 bytes while PORT exits 0 (silent data loss); in active mode over TLS 1.3 a strict server answers 426 and PORT exits 18

- **Severity:** Critical
- **Area:** ftp-file-tftp, tls-protocols-revocation
- **Platform(s):** Windows. Detail: [ftp-file-tftp FTP-2] Windows (the same case on Linux matches REF)
- **Confidence:** Confirmed. Detail: [ftp-file-tftp FTP-2] Confirmed (reproduced twice here, plus the original runs)
- **Related existing finding (GF/AF):** none
- **Source findings:** `ftp-file-tftp FTP-2`, `tls-protocols-revocation TPR-05`

Both findings point at the same close path after `SecureDataConnectionAsync` (`Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs`, about line 1757). The likely cause is closing the socket with unread TLS 1.3 post-handshake data (NewSessionTicket) or without close_notify, which makes Windows send RST.

**Reproduction**

- *Setup (ftp-file-tftp):* EV = `SCR\evidence\ftp-file-tftp`. Scripted FTP/TFTP stub: `EV\stub\win3\stub.exe` (Linux `EV/stub/linux/stub`, source `EV\stub\Program.cs`) with fixture root `EV\fixture\root` (file.txt is 18 bytes). Differential harness `EV\dt.sh NAME 'script' curl-args`, where `@U@` = `ftp://127.0.0.1:$CPORT`, script lines are `CMD=reply` or `OPT key=value` separated by `;;`, and `STUBEXTRA=EV\tls.pfx` enables FTPS. Verifier: `EV\v1.sh`, `EV\v2.sh` (Windows), `EV\vl.sh` (Linux).
- *Setup (tls-protocols-revocation):* E = `SCR\evidence\tls-protocols-revocation`; TP = `SCR\evidence\tls\pki` (srv.crt/srv.key/srv.pfx password `x`, ca.crt for CN localhost). Server `E\srv\out\tsrv.exe <smtp|imap|pop3|ftp|ldap|http|proxy|crl> <port> pfx=<pfx> [tls=implicit|starttls|none] [tls12=1] [strict=1] [updir=<dir>] [mode=reneg|nonotify|closedelim] [www=<dir>]` (Linux `E\srv\outlinux\tsrv`; it logs every client message, `Cs:` = inside TLS). Also `openssl s_server`. Revocation PKI `E\pki` (`rca.crt`; `ocg*` good, `ocr*` revoked with staples `certs\<tag>.ocsp`; `rev*` with CDP on port 21990). Harness `E\h.ps1`, `E\linux\lh.sh`; verifier `E\v.ps1`, `E\linux\lv.sh`.
- **[ftp-file-tftp FTP-2]** FTPS upload on Windows loses all data while the exit code is 0
  - Stub with `EV\tls.pfx` and script `OPT eagertls=1`. Run `curl -sS -k --ssl-reqd -T upload.txt ftp://127.0.0.1:20960/u.txt`, where upload.txt contains `up1\nup2\n` (8 bytes). Cases: `v_se_up` and `v_se_up2` (`EV\v1.sh`); originals `EV\cases\se_up_r2`, `se_up_r3`, `se_reqd_up`.
- **[tls-protocols-revocation TPR-05]** Windows: FTPS uploads in active mode over TLS 1.3 end the data connection with a TCP reset, so a strict server answers 426 and PORT exits 18
  - `E\srv\out\tsrv.exe ftp 21900 pfx=TP\srv.pfx updir=E\up tls=implicit strict=1`. With `strict=1` the server answers `426 Failure reading network stream.` when the data connection ends with an error, as vsftpd does. Then run `curl -sS -k -P 127.0.0.1 -T up.txt ftps://localhost:21900/new.txt`. Explicit variant: the server without `tls=implicit`, client `curl -sS -k --ssl-reqd -P 127.0.0.1 -T up.txt ftp://localhost:21900/new.txt`. Verifier cases: `v_P_stor_strict`, `v_P_stor_reqd_strict`, plus the controls `v_P_stor12_strict` (TLS 1.2, matches) and Linux `V_P_stor_strict` (matches).

Commands (run each with REF and with PORT):

```sh
# [ftp-file-tftp FTP-2]
curl -sS -k --ssl-reqd -T upload.txt ftp://127.0.0.1:20960/u.txt
EV\v1.sh
# [tls-protocols-revocation TPR-05]
E\srv\out\tsrv.exe ftp 21900 pfx=TP\srv.pfx updir=E\up tls=implicit strict=1
curl -sS -k -P 127.0.0.1 -T up.txt ftps://localhost:21900/new.txt
curl -sS -k --ssl-reqd -P 127.0.0.1 -T up.txt ftp://localhost:21900/new.txt
```

**Expected (real curl)**

- **[ftp-file-tftp FTP-2]** The server receives the 8 bytes and replies 226. u.txt holds `up1\nup2\n`. Exit 0.
- **[tls-protocols-revocation TPR-05]** The server reads the whole upload with a clean end of stream and answers 226. Exit 0.

**Actual (Curl)**

- **[ftp-file-tftp FTP-2]** The server log shows `DATA recv error after 0 bytes: IOException: ... An existing connection was forcibly closed by the remote host`, followed by `DATA recv 0 bytes`. u.txt on the server is empty, yet PORT exits 0 and sends QUIT. The only harness diff is ROOTFILES, so the upload looks successful. The data connection is reset before the server reads any application data.
  - Suspected cause: the socket is closed while TLS 1.3 post-handshake data (the session ticket) is unread, which makes Windows send RST, or it is closed without close_notify.
  - Linux `v_se_up` is SAME.
- **[tls-protocols-revocation TPR-05]** The 14 upload bytes arrive (same SHA), but the server's read ends with `IOException: … An existing connection was forcibly closed by the remote host` (RST). The strict server answers 426, and PORT exits 18 `curl: (18) server did not report OK, got 426`. Against a lenient server the exit is 0, but the reset is still on the wire (original `b2_*_P_stor*`, `b2_reqd_P_stdin`).

**Suspected location in source**

- **[ftp-file-tftp FTP-2]** `Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs`, the upload data-connection close path after `SecureDataConnectionAsync` (`:1757`). Not narrowed further.
- **[tls-protocols-revocation TPR-05]** `Curl.Protocol.Ftp.UnitLibrary\FtpSession.cs`, the upload data-connection close after `SecureDataConnectionAsync` (about line 1757). The likely cause is closing the socket with unread TLS 1.3 post-handshake data (NewSessionTicket), which makes Windows send RST. Not narrowed further.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr006Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-006 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
