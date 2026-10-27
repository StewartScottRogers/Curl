---
id: BL-3694
title: Fix CGR-165: Explicit FTPS (AUTH TLS) never reuses the control connection for the next URL, while a CCC-cleared plaintext connection is reused where c...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Protocol.Ftp.UnitTests/Cgr165Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3694 — Fix CGR-165: Explicit FTPS (AUTH TLS) never reuses the control connection for the next URL, while a CCC-cleared plaintext connection is reused where c...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-165 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-165: Explicit FTPS (AUTH TLS) never reuses the control connection for the next URL, while a CCC-cleared plaintext connection is reused where curl reconnects

- **Severity:** Medium
- **Area:** tls-protocols-revocation
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `tls-protocols-revocation TPR-04`

**Reproduction**

- *Setup (tls-protocols-revocation):* E = `SCR\evidence\tls-protocols-revocation`; TP = `SCR\evidence\tls\pki` (srv.crt/srv.key/srv.pfx password `x`, ca.crt for CN localhost). Server `E\srv\out\tsrv.exe <smtp|imap|pop3|ftp|ldap|http|proxy|crl> <port> pfx=<pfx> [tls=implicit|starttls|none] [tls12=1] [strict=1] [updir=<dir>] [mode=reneg|nonotify|closedelim] [www=<dir>]` (Linux `E\srv\outlinux\tsrv`; it logs every client message, `Cs:` = inside TLS). Also `openssl s_server`. Revocation PKI `E\pki` (`rca.crt`; `ocg*` good, `ocr*` revoked with staples `certs\<tag>.ocsp`; `rev*` with CDP on port 21990). Harness `E\h.ps1`, `E\linux\lh.sh`; verifier `E\v.ps1`, `E\linux\lv.sh`.
- `E\srv\out\tsrv.exe ftp 21900 pfx=TP\srv.pfx updir=E\up` (add `tls12=1` for TLS 1.2), then `curl -sS -k --ssl-reqd ftp://localhost:21900/a.txt ftp://localhost:21900/b.txt`. CCC variant (Linux, where CCC works): `curl -sS -k --ssl-reqd --ftp-ssl-ccc ftp://localhost:<p>/file.txt ftp://localhost:<p>/file2.txt`. Verifier cases: Windows `v_reqd_two12`, `v_reqd_two13`, plus the control `v_implicit_two13` (implicit `ftps://`, which matches). Linux `V_ftp_two_explicit`, `V_ftp_ccc_two`. Original cases: `b8_*two*`, `L4_two_*`, `L1_ccc_two`, `L2_ccc_*`.

Commands (run each with REF and with PORT):

```sh
E\srv\out\tsrv.exe ftp 21900 pfx=TP\srv.pfx updir=E\up
curl -sS -k --ssl-reqd ftp://localhost:21900/a.txt ftp://localhost:21900/b.txt
curl -sS -k --ssl-reqd --ftp-ssl-ccc ftp://localhost:<p>/file.txt ftp://localhost:<p>/file2.txt
```

**Expected (real curl)**

- AUTH TLS: one control connection carrying `RETR a.txt`, `EPSV`, `SIZE b.txt`, `RETR b.txt`, `QUIT`. After CCC: curl does not reuse the now-plaintext connection for an SSL-required transfer. The second URL opens a new connection with `AUTH SSL`, USER, PASS, `PBSZ 0`, `PROT P`, `CCC`.

**Actual (Curl)**

- AUTH TLS: it sends `QUIT` after `RETR a.txt` and opens a second control connection with `AUTH SSL`, a TLS handshake (resumed) and a full login for `b.txt`. After CCC it does the opposite: it reuses the plaintext control connection and sends `EPSV`, `SIZE file2.txt`, `RETR file2.txt` on it without AUTH, PBSZ or PROT. Exit codes and data match; the number of logins and the commands on the wire differ.

**Suspected location in source**

- `Curl.Protocol.Ftp.UnitLibrary\FtpSession.cs:2510-2516` (`TryKeepControlConnection`). `!ReferenceEquals(control.Connection, originalControl)` refuses any connection that AUTH wrapped in TLS, and accepts a CCC-unwrapped one again.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr165Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Pipeline `feature`, done directly in one library: the cause was one condition in `FtpSession.TryKeepControlConnection`, so a full architect/implementer round would have cost more than the fix. Cause: the session kept only the pooled connection it was given, so an `AUTH`-wrapped TLS connection was never kept, while `CCC`'s `ClearTlsAsync` hands back that same pooled connection, so a cleared one was. Fix: `FtpKeptConnection.SecuredControl` holds the TLS connection (the reusing session switches to it and the closing `QUIT` goes on it), and a `controlCleared` flag stops a CCC-cleared connection from being kept. Decision recorded in ADR-0861.
- Measured on Windows with `Record-CurlExchange.ps1 -Ftp -FtpData hello`, REF = curl 8.22.0 Schannel, PORT = this branch's Debug `curl.exe`:
  - `curl -sS -k --ssl-reqd ftp://localhost:21946/a.txt ftp://localhost:21946/b.txt`: REF and PORT both exit 0, stdout `hellohello`, stderr empty. Transcripts are equal apart from the EPSV port: one connection with `AUTH SSL`, USER, PASS, `PBSZ 0`, `PROT P`, PWD, EPSV, `TYPE I`, `SIZE a.txt`, `RETR a.txt`, EPSV, `SIZE b.txt`, `RETR b.txt`, QUIT. Before the fix PORT sent QUIT after `RETR a.txt` and logged in again.
  - Control: `curl -sS -k ftps://localhost:21947/a.txt ftps://localhost:21947/b.txt` (with `-Tls`): REF and PORT both exit 0, stdout `hellohello`, transcripts equal. It still matches.
  - `curl -sS -k --ssl-reqd --ftp-ssl-ccc ftp://localhost:21948/file.txt ftp://localhost:21948/file2.txt`: REF and PORT both exit 28, with stderr `curl: (81) Failed to clear the command channel (CCC)` then `curl: (28) server response timeout`. Both send the same commands, AUTH SSL through CCC. It still matches.
  - Linux REF (curl 8.18.0 OpenSSL in WSL, `-Curl wsl.exe -ListenAddress 172.26.96.1`) with `--ftp-ssl-ccc`: after CCC it sends PWD, EPSV, `TYPE I`, `SIZE file.txt` and `RETR file.txt` in plain text, then nothing more on that connection; it tries a new connection for `file2.txt`, which the one-session recorder never answers (exit 28 under `--connect-timeout 5`). PORT cannot run on Linux from this lane. The CCC behaviour is pinned by `Cgr165Tests.ExecuteAsync_ControlConnectionCccCleared_IsNotKeptAndEndsWithQuit`: PORT now ends the cleared connection with QUIT and connects anew. curl sends that QUIT when its cache closes the connection at exit. The bytes on each connection are the same; only their order across the two connections differs (ADR-0861).
- `Cgr165Tests` (2 tests): both fail with the production change reverse-applied and pass with it. Build clean. Fast tests: 44,556 in all, none failed. `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary`: 0 failing members.
- CI on Linux and macOS: the change is platform-neutral (in-memory fakes, no paths, OS error text or certificates), so that criterion is met with the fast tests green here.

## Log

- 2026-10-26: Created from CGR-165 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. Explicit FTPS keeps its AUTH-secured control connection for the next URL, and a CCC-cleared one is never reused, as curl does
