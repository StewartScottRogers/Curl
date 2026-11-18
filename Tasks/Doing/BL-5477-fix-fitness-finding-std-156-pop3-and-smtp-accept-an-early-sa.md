---
id: BL-5477
title: Fix fitness finding STD-156: POP3 and SMTP accept an early SASL success before the client has finished (LOGIN with --sasl-ir; POP3 CRAM-MD5 with --sasl-ir); curl fails with "Login denied"
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Pop3.UnitLibrary/Pop3Login.cs, Curl.Original.Protocol.Smtp.UnitLibrary/SmtpSaslAuthentication.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5477 — Fix fitness finding STD-156: POP3 and SMTP accept an early SASL success before the client has finished (LOGIN with --sasl-ir; POP3 CRAM-MD5 with --sasl-ir); curl fails with "Login denied"

## Goal

STD-156 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: POP3 and SMTP accept an early SASL success before the client has finished (LOGIN with --sasl-ir; POP3 CRAM-MD5 with --sasl-ir); curl fails with "Login denied". Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low (neighbour of the fixed IMAP issue STD-105; a server that skips authentication gets mail sent or retrieved where curl stops)
- **Confidence:** Confirmed (2 runs, two servers each)
- **Protocol/area:** POP3 / SMTP SASL, Windows
- **Repro:** POP3 mock on 127.0.0.1:44211 (greeting `+OK ready <1.2@x>`; `CAPA` → `+OK`, `SASL PLAIN LOGIN CRAM-MD5 XOAUTH2 OAUTHBEARER`, `USER`, `.`; any `AUTH …` → `+OK auth` at once, no `+ ` continuation; `RETR` → a message). SMTP mock on 127.0.0.1:44220 (`220 ready`; `EHLO` → `250-hi`, `250-AUTH PLAIN LOGIN CRAM-MD5 XOAUTH2 OAUTHBEARER`, `250 SIZE 1000`; any `AUTH …` → `235 ok` at once; `MAIL`/`RCPT` → `250`; `DATA` → `354`/`250`).
  - `curl -sS -m 10 --sasl-ir -u user:pw --login-options AUTH=LOGIN pop3://127.0.0.1:44211/1`
  - `curl -sS -m 10 --sasl-ir -u user:pw --login-options AUTH=CRAM-MD5 pop3://127.0.0.1:44211/1`
  - `curl -sS -m 10 --sasl-ir -u user:pw --login-options AUTH=LOGIN --mail-from a@b --mail-rcpt c@d -T a.txt smtp://127.0.0.1:44220`
- **Expected (REF):** each prints `curl: (67) Login denied`, exit 67, sending nothing after `AUTH LOGIN dXNlcg==` / `AUTH CRAM-MD5`.
- **Actual (PORT):** POP3 sends `RETR 1` and `QUIT`, prints the message, exit 0. SMTP sends `MAIL FROM`, `RCPT TO`, `DATA` and the body, exit 0. Without `--sasl-ir`, and for PLAIN/OAUTHBEARER and SMTP CRAM-MD5, the PORT matches.
- **Likely location:** `Curl.Original.Protocol.Pop3.UnitLibrary/Pop3Login.cs` `NtlmUnfinished` (~line 316) guards only NTLM; `Curl.Original.Protocol.Smtp.UnitLibrary/SmtpSaslAuthentication.cs` `Outcome` (~line 376). Apply the IMAP fix (`HasFinished`): LOGIN is finished only after the password prompt is answered, CRAM-MD5 only after its challenge.
- **Acceptance check:** the three commands exit 67 with `curl: (67) Login denied`; the servers see no `RETR` or `MAIL`.

## Acceptance criteria

- [ ] the three commands exit 67 with `curl: (67) Login denied`; the servers see no `RETR` or `MAIL`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-156 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 8a9d20cb 69116afe 3d836d5e.
