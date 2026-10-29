---
id: BL-3753
title: Fix CGR-224: TLS handshake failure text: PORT prints a generic failed to receive handshake (Windows) or a .NET frame-size message (Linux) instead of t...
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Networking.UnitTests/Cgr224Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3753 — Fix CGR-224: TLS handshake failure text: PORT prints a generic failed to receive handshake (Windows) or a .NET frame-size message (Linux) instead of t...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-224 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-224: TLS handshake failure text: PORT prints a generic `failed to receive handshake` (Windows) or a .NET frame-size message (Linux) instead of the Schannel SEC_E_* / OpenSSL reason

- **Severity:** Low
- **Area:** tls, writeout-verbose
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `tls TLS-12`, `writeout-verbose WV-16`

**Reproduction**

- *Setup (tls):* E = `SCR\evidence\tls`. PKI in `E\pki` (`index.txt` contains `hello tls`; pfx password `x`). Servers: `openssl s_server -accept <p> -cert E\pki\srv.crt -key E\pki\srv.key -WWW [options]` (Git ucrt64 openssl on Windows, system openssl in WSL, run from a directory containing `index.txt`), `E\srv\out\starttls.exe <proto> <port> <pfx> [implicit|refuse|noext]` (source `E\srv\starttls.cs`), `E\linux\stsrv.py` (same idea in Python). Harnesses `E\tlsdiff.ps1`, `E\linux\tlsdiff.sh`; verifier `E\verify\vwin1.ps1`, `vwin2.ps1`, `vlin.sh` (cases `E\cases\V_*`, `E\linux\cases\V_L_*`).
- *Setup (writeout-verbose):* EV = `SCR\evidence\writeout-verbose`. Windows server: `cd EV\srv && dotnet run wvsrv.cs -- 20700 EV\resp EV\req.log` (HTTP on 127.0.0.1/::1:20700, a mini FTP server on 20701, TLS on 20702; `/raw/<name>` serves `EV\resp\<name>.txt`; other routes `/code/N`, `/redir/N`, `/hang/ms`, `/close` (empty reply), `/ka`, `/echo`, `/p`). Linux: `EV\linux\srv.py` on 20710. One-line repros for every finding: `EV\repro_findings.sh` (Windows) and `EV\linux\verify_linux.sh`.
- **[tls TLS-12]** Windows handshake-failure text: PORT prints 'failed to receive handshake' where Schannel REF prints SEC_E_UNSUPPORTED_FUNCTION / SEC_E_ILLEGAL_MESSAGE / SEC_E_INVALID_TOKEN
  - (a) `openssl s_server ... -WWW`, then `curl -sS -k --tls-max 1.0 https://localhost:<p>/index.txt`. (b) Server `-tls1_2`, then `curl -sS -k --tlsv1.0 --tls-max 1.1 ...`. (c) A non-TLS server (`starttls.exe smtp <p> srv.pfx`), then `curl -sS -k https://localhost:<p>/`. Cases: `V_max10`, `V_srv12_tlsv10_max11`, `V_garbage`.
- **[writeout-verbose WV-16]** The TLS error text differs when the server answers the handshake in plaintext
  - `curl -sS -m 5 https://127.0.0.1:20700/` against the plain HTTP port.

Commands (run each with REF and with PORT):

```sh
# [tls TLS-12]
openssl s_server ... -WWW
curl -sS -k --tls-max 1.0 https://localhost:<p>/index.txt
curl -sS -k --tlsv1.0 --tls-max 1.1 ...
curl -sS -k https://localhost:<p>/
# [writeout-verbose WV-16]
curl -sS -m 5 https://127.0.0.1:20700/
```

**Expected (real curl)**

- **[tls TLS-12]** (a) `curl: (35) schannel: next InitializeSecurityContext failed: SEC_E_UNSUPPORTED_FUNCTION (0x80090302) - The function requested is not supported`. (b) `... SEC_E_ILLEGAL_MESSAGE (0x80090326) - This error usually occurs when a fatal SSL/TLS alert is received ...`. (c) `... SEC_E_INVALID_TOKEN (0x80090308) - The token supplied to the function is invalid`.
- **[writeout-verbose WV-16]** 
  - Windows: `curl: (35) schannel: next InitializeSecurityContext failed: SEC_E_INVALID_TOKEN (0x80090308) - The token supplied to the function is invalid`
  - Linux: `curl: (35) TLS connect error: error:0A00010B:SSL routines::wrong version number`

**Actual (Curl)**

- **[tls TLS-12]** `curl: (35) schannel: failed to receive handshake, SSL/TLS connection failed` in every case. The exit code matches.
- **[writeout-verbose WV-16]** 
  - Windows: `curl: (35) schannel: failed to receive handshake, SSL/TLS connection failed`
  - Linux: `curl: (35) TLS connect error: Cannot determine the frame size or a corrupted frame was received.` This is a .NET exception message.

**Suspected location in source**

- **[tls TLS-12]** `Curl.Networking.UnitLibrary\TlsFailureMessages.cs:56` (`SchannelHandshakeNotReceived`) and its selection at about lines 372-392.
- **[writeout-verbose WV-16]** `Curl.Networking.UnitLibrary/TlsFailureMessages.cs`

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr224Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-224 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
