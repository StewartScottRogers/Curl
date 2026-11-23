---
id: BL-5810
title: Fix fitness finding STD-166: POP3, SMTP and IMAP accept a premature SASL success during DIGEST-MD5 (and POP3 with --sasl-ir before any challenge); curl fails with "Login denied"
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Pop3.UnitLibrary/Pop3Login.cs, Curl.Original.Protocol.Imap.UnitLibrary/ImapAuthentication.cs, Curl.Original.Protocol.Smtp.UnitLibrary/SmtpSaslAuthentication.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5810 — Fix fitness finding STD-166: POP3, SMTP and IMAP accept a premature SASL success during DIGEST-MD5 (and POP3 with --sasl-ir before any challenge); curl fails with "Login denied"

## Goal

STD-166 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: POP3, SMTP and IMAP accept a premature SASL success during DIGEST-MD5 (and POP3 with --sasl-ir before any challenge); curl fails with "Login denied". Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low (same class as the fixed STD-156: a server that skips the end of authentication gets mail sent or retrieved where curl stops)
- **Confidence:** Confirmed (2 runs on Windows; also reproduced on the Linux build vs REF2)
- **Protocol/area:** POP3, SMTP, IMAP SASL DIGEST-MD5, Windows and Linux builds
- **Repro:**
  1. Mock servers (POP3 on 25018, SMTP on 25028, IMAP on 25038) that advertise `PLAIN LOGIN CRAM-MD5 DIGEST-MD5 NTLM XOAUTH2 OAUTHBEARER EXTERNAL`, send the DIGEST-MD5 challenge `realm="x",nonce="abc",qop="auth",algorithm=md5-sess,charset=utf-8`, read the client's digest response, and then answer success (`+OK auth` / `235 ok` / `A00n OK auth`) instead of the `rspauth` continuation. A fourth POP3 mock on 25011 answers `+OK auth` immediately to `AUTH …`. RETR, MAIL, RCPT, DATA and UID FETCH succeed.
  2. `a.txt` contains `hello\r\n`.
  3. `curl -sS -m 8 -u user:pw --login-options AUTH=DIGEST-MD5 pop3://127.0.0.1:25018/1`
  4. `curl -sS -m 8 -u user:pw --login-options AUTH=DIGEST-MD5 --mail-from a@b --mail-rcpt c@d -T a.txt smtp://127.0.0.1:25028`
  5. `curl -sS -m 8 -u user:pw --login-options AUTH=DIGEST-MD5 "imap://127.0.0.1:25038/INBOX/;UID=1"`
  6. `curl -sS -m 8 --sasl-ir -u user:pw --login-options AUTH=DIGEST-MD5 pop3://127.0.0.1:25011/1`
- **Expected (REF):** each prints `curl: (67) Login denied`, exit 67, and sends nothing after the digest response (steps 3–5) or after `AUTH DIGEST-MD5` (step 6).
- **Actual (.NET):** exit 0 in all four. POP3 sends `RETR 1` and prints the message; SMTP sends `MAIL FROM:<a@b> SIZE=7`, `RCPT TO:<c@d>`, `DATA` and the body; IMAP sends `SELECT` and `UID FETCH`. LOGIN, NTLM and EXTERNAL in the same positions now correctly give 67.
- **Likely location:** `Curl.Original.Protocol.Pop3.UnitLibrary/Pop3Login.cs` `NtlmUnfinished` (~line 316; covers NTLM/LOGIN/CRAM-MD5 only while `challengesHanded == 0`, not DIGEST-MD5); `Curl.Original.Protocol.Imap.UnitLibrary/ImapAuthentication.cs` `HasFinished` (~line 366); `Curl.Original.Protocol.Smtp.UnitLibrary/SmtpSaslAuthentication.cs` `Outcome` (~lines 251/376). DIGEST-MD5 is finished only after the `rspauth` continuation has been answered (`challengesHanded >= 2`); a success before that is "Login denied".
- **Acceptance check:** steps 3–6 each exit 67 with `curl: (67) Login denied`, and the mock logs show no `RETR`, `MAIL` or `SELECT` after the AUTH exchange.

## Acceptance criteria

- [ ] steps 3–6 each exit 67 with `curl: (67) Login denied`, and the mock logs show no `RETR`, `MAIL` or `SELECT` after the AUTH exchange.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.