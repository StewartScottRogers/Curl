---
id: BL-5473
title: Fix fitness finding STD-152: POP3, IMAP, SMTP (plain and TLS) and RTSP report %{time_pretransfer}, %{time_posttransfer} and %{time_starttransfer} as 0
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Pop3.UnitLibrary/Pop3Session.cs, Curl.Original.Protocol.Pop3.UnitLibrary/Pop3ProtocolHandler.cs, Curl.Original.Protocol.Imap.UnitLibrary/ImapSession.cs, Curl.Original.Protocol.Imap.UnitLibrary/ImapProtocolHandler.cs, Curl.Original.Protocol.Smtp.UnitLibrary/SmtpSession.cs, Curl.Original.Protocol.Smtp.UnitLibrary/SmtpProtocolHandler.cs, Curl.Original.Protocol.Rtsp.UnitLibrary/RtspProtocolHandler.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5473 — Fix fitness finding STD-152: POP3, IMAP, SMTP (plain and TLS) and RTSP report %{time_pretransfer}, %{time_posttransfer} and %{time_starttransfer} as 0

## Goal

STD-152 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: POP3, IMAP, SMTP (plain and TLS) and RTSP report %{time_pretransfer}, %{time_posttransfer} and %{time_starttransfer} as 0. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** pop3/pop3s/imap/smtp/rtsp write-out, Windows
- **Repro:** mocks: POP3 on 127.0.0.1:44212 (`+OK` greeting; `CAPA` lists `USER`; `USER`/`PASS` → `+OK`; `RETR` → a 36-byte message); IMAP on 44203 (`* OK`; `CAPABILITY` with `AUTH=CRAM-MD5`; auth accepted; `SELECT` → `* 1 EXISTS`; `UID FETCH` → a 36-byte literal); SMTP on 44222 (`220`; `EHLO`; `MAIL`/`RCPT` → `250`; `DATA` → `354`/`250`); POP3S on localhost:44312 (implicit TLS, certificate from a test CA `ca.pem`); the RTSP mock from STD-074. `W` as in STD-150.
  - `curl -s -u u:p -o NUL -w "$W" pop3://127.0.0.1:44212/1`
  - `curl -s -u u:p -o NUL -w "$W" "imap://127.0.0.1:44203/INBOX/;UID=1"`
  - `curl -s -u u:p -T a.txt --mail-rcpt c@d -w "$W" smtp://127.0.0.1:44222`
  - `curl -s --cacert ca.pem --ssl-no-revoke -u u:p -o NUL -w "$W" pop3s://localhost:44312/1`
  - `curl -s -o NUL -w "$W" rtsp://127.0.0.1:44542/m`
- **Expected (REF):** POP3 `c=0.000800 pre=0.005770 post=0.005770 st=0.005779 tot=0.005820`; IMAP `pre=0.002799 post=0.002799 st=0.002808`; SMTP `pre=0.002472 post=0.002576 st=0.002639`; POP3S `pre=0.029231 post=0.029231 st=0.029258`; RTSP `pre=0.000843 post=0.000843 st=0.001246`.
- **Actual (PORT):** all five print `pre=0.000000 post=0.000000 st=0.000000` with non-zero `c` and `tot` (`time_appconnect` is correct for POP3S).
- **Likely location:** the report builders in `Curl.Original.Protocol.{Pop3,Imap,Smtp,Rtsp}.UnitLibrary` (`*Session.cs` / `*ProtocolHandler.cs`) build `TransferReport` without those stamps; FTP's `TimingsAt` and SMB's `StampReport` show the pattern.
- **Acceptance check:** all five print non-zero `pre`, `post` and `st` with `c ≤ pre ≤ st ≤ tot`.

## Acceptance criteria

- [ ] all five print non-zero `pre`, `post` and `st` with `c ≤ pre ≤ st ≤ tot`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-152 (Low).