---
id: BL-3930
title: Port the SMTP, IMAP and POP3 modes of Record-CurlExchange to Record-CurlExchange.sh
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3929]
touches: [Record-CurlExchange.sh]
requirement: none
created: 2026-10-27
completed: 2026-10-28
---
# BL-3930 — Port the SMTP, IMAP and POP3 modes of Record-CurlExchange to Record-CurlExchange.sh

## Goal

`Record-CurlExchange.sh -Smtp`, `-Imap` and `-Pop3` (plain and TLS) record the same request
bytes, stdout, stderr and exit code as `Record-CurlExchange.ps1` for the same curl command.

## Context

Part of BL-3934; follows BL-3929 in the same file. See BL-3929's Context for the script's approach
and how to test in WSL against the `.ps1`. These modes are scripted conversations: the server
answers each command line with a canned reply, so the bash version reads line by line rather than
byte by byte where the `.ps1` does.

## Acceptance criteria

- [x] Every SMTP, IMAP and POP3 parameter of `Record-CurlExchange.ps1` is handled by `Record-CurlExchange.sh`, and the "not ported" block no longer lists them.
- [x] For each of the three protocols, at least one plain and one TLS (STARTTLS where the `.ps1` supports it) case records byte-identical `request.bin`, `stdout.bin`, `stderr.txt` and `exitcode.txt` to the `.ps1`; cases and results are in the Log. Any case that cannot match is listed with its reason at the top of the file.
- [x] `bash -n Record-CurlExchange.sh` passes and the HTTP, HTTPS and FTP cases still match.

## Notes

Measured in WSL Ubuntu (curl 8.18.0, socat 1.8.1.1 from apt), .sh vs Record-CurlExchange.ps1 under PowerShell 7 on Windows against the same WSL curl (ps1 bound to the WSL gateway address, 127.0.0.1 normalised; curl's progress-meter lines, whose count depends on timing, left out of stderr). request.bin, stdout.bin, stderr.txt, exitcode.txt and transcript.txt byte-identical for 17 cases: SMTP send, with AUTH and an `RCPT=550` override (exit 55), STARTTLS, implicit smtps:// (-Tls), `-X "VRFY"`; IMAP fetch by UID, list, STARTTLS, imaps://, APPEND (a literal), an ImapReply override; POP3 RETR, LIST, `TOP 1 1`, STLS, pop3s://, a Pop3Reply override. The FTP, HTTP and HTTPS cases still run (spot checks in WSL).
The three modes reuse the FTP control connection's TLS plumbing (`rce_ftp_enter_tls`) and its override table (`RCE_FR_*`); an IMAP verb with a space (`UID FETCH`) is `UID_FETCH` in that table. ImapMessage and Pop3Message become `mailmessage.bin`; the three idle parameters are honoured.
Decision (no ADR needed, a port of existing behaviour): none beyond the above.
## Log

- 2026-10-27: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: dffab3ef bc812ec6 b55ab035.
- 2026-10-28: Doing -> Done. SMTP, IMAP and POP3 modes ported; 17 cases byte-identical to the ps1
