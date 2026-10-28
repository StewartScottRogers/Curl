---
id: BL-3742
title: Fix CGR-213: -m timeout in non-HTTP protocols (SMTP, POP3, IMAP, SFTP, SCP) reports Connection timed out after N milliseconds instead of Operation tim...
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/MaxTimeWatchdog.cs, Curl.Core.UnitTests/Cgr213Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3742 — Fix CGR-213: -m timeout in non-HTTP protocols (SMTP, POP3, IMAP, SFTP, SCP) reports Connection timed out after N milliseconds instead of Operation tim...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-213 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-213: `-m` timeout in non-HTTP protocols (SMTP, POP3, IMAP, SFTP, SCP) reports `Connection timed out after N milliseconds` instead of `Operation timed out after N milliseconds with X bytes received`

- **Severity:** Low
- **Area:** writeout-verbose, other-protocols, sftp-scp-ssh
- **Platform(s):** Both (Windows and Linux). Detail: [sftp-scp-ssh SSH-09] Windows, Linux
- **Confidence:** Confirmed. Detail: [writeout-verbose WV-02] Confirmed (Windows and Linux).; [other-protocols OP-13] Confirmed (Windows and Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `writeout-verbose WV-02`, `other-protocols OP-13`, `sftp-scp-ssh SSH-09`

**Reproduction**

- *Setup (writeout-verbose):* EV = `SCR\evidence\writeout-verbose`. Windows server: `cd EV\srv && dotnet run wvsrv.cs -- 20700 EV\resp EV\req.log` (HTTP on 127.0.0.1/::1:20700, a mini FTP server on 20701, TLS on 20702; `/raw/<name>` serves `EV\resp\<name>.txt`; other routes `/code/N`, `/redir/N`, `/hang/ms`, `/close` (empty reply), `/ka`, `/echo`, `/p`). Linux: `EV\linux\srv.py` on 20710. One-line repros for every finding: `EV\repro_findings.sh` (Windows) and `EV\linux\verify_linux.sh`.
- *Setup (other-protocols):* EV = `SCR\evidence\other-protocols`. Scripted per-protocol loopback servers and runner in `EV\bench\Program.cs`; Windows `pwsh -File EV\run.ps1 <file>.cases [filter]`, Linux `bash EV/run.sh <file>.cases` (set `FILTER=`) or `EV/verify-linux.sh [filter]`. Each `.cases` line is `name | server-mode | server options | curl args`. Default servers: SMTP answers 250 and offers AUTH PLAIN LOGIN CRAM-MD5 XOAUTH2 OAUTHBEARER; POP3 offers SASL PLAIN LOGIN CRAM-MD5; IMAP offers AUTH=PLAIN AUTH=LOGIN AUTH=CRAM-MD5 SASL-IR.
- *Setup (sftp-scp-ssh):* E = `SCR\evidence\sftp-scp-ssh`. Server: a throwaway non-root OpenSSH 10.2p1 sshd in WSL on 127.0.0.1:21523 (`E\setup.sh`, then `E\setup2.sh`; `E\remote2.sh reset` rebuilds the test tree; Windows reaches it through WSL localhost forwarding). Odd servers: `E\oddstart.sh` (21531 accepts and stays silent 60 s; 21533 sends `SSH-1.5-old` and then nothing). `AUTH` = `-k -u stewart_rogers: --key E\keys\c_rsa_pem --pubkey E\keys\c_rsa_pem.pub`; `U` = `sftp://127.0.0.1:21523/home/stewart_rogers/sftpt21501`; `UC` = the same with `scp://`. Verifier: `E\verify\vwin.sh`, `E\verify\vlin.sh`, `E\verify\vwinlog.sh`.
- **[writeout-verbose WV-02]** POP3/IMAP/SMTP with -m during the greeting report "Connection timed out" instead of "Operation timed out"
  - Same silent server as WV-01. `curl -sS -w '%{errormsg}\n' pop3://127.0.0.1:20700/ -m 2`
- **[other-protocols OP-13]** `-m` timeout during SMTP/POP3/IMAP commands prints "Connection timed out" instead of "Operation timed out ... with 0 bytes received"
  - `EV\confirm.cases` C16, with server option `rule1=EHLO=>{SLEEP}250 ok\r\n` (the server delays its reply by 2 s). Also `EV\wvars.cases` v19-v23 and v30. Command: `curl -s -S -m 1 smtp://127.0.0.1:21001 --mail-rcpt x`
- **[sftp-scp-ssh SSH-09]** --max-time during an sftp/scp transfer prints 'Connection timed out after N milliseconds'
  - `curl -sS AUTH --max-time 2 --limit-rate 50K -o NUL U/big.bin`. Repeat with `UC/big.bin`.

Commands (run each with REF and with PORT):

```sh
# [writeout-verbose WV-02]
curl -sS -w '%{errormsg}\n' pop3://127.0.0.1:20700/ -m 2
# [other-protocols OP-13]
curl -s -S -m 1 smtp://127.0.0.1:21001 --mail-rcpt x
# [sftp-scp-ssh SSH-09]
curl -sS AUTH --max-time 2 --limit-rate 50K -o NUL U/big.bin
```

**Expected (real curl)**

- **[writeout-verbose WV-02]** `curl: (28) Operation timed out after 2005 milliseconds with 0 bytes received`. `%{errormsg}` gives the same text.
- **[other-protocols OP-13]** `curl: (28) Operation timed out after 1015 milliseconds with 0 bytes received`. When the reply after end-of-data is late it prints `curl: (28) server response timeout`.
- **[sftp-scp-ssh SSH-09]** exit 28 with `curl: (28) Operation timed out after 2002 milliseconds with 104448 out of 1048576 bytes received`.

**Actual (Curl)**

- **[writeout-verbose WV-02]** `curl: (28) Connection timed out after 2010 milliseconds`. `%{errormsg}` gives the same text.
- **[other-protocols OP-13]** `curl: (28) Connection timed out after 1007 milliseconds` in every phase. Exit code 28 matches.
- **[sftp-scp-ssh SSH-09]** exit 28 with `curl: (28) Connection timed out after 2014 milliseconds`, although about 100 KB of the body had already arrived.

**Suspected location in source**

- **[writeout-verbose WV-02]** `Curl.Core.UnitLibrary/MaxTimeWatchdog.cs:86-99` picks `ConnectionTimedOutMessage` when `transferStarted` is false. The mail handlers never mark the transfer as started.
- **[other-protocols OP-13]** `Curl.Core.UnitLibrary\MaxTimeWatchdog.cs:86-189`.
- **[sftp-scp-ssh SSH-09]** `Curl.Core.UnitLibrary/MaxTimeWatchdog.cs:86/189` picks the "Connection" wording because the SSH handler never reports that the transfer has started or how much has arrived.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr213Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-213 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
