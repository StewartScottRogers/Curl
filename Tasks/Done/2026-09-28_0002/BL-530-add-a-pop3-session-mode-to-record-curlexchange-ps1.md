---
id: BL-530
title: Add a -Pop3 session mode to Record-CurlExchange.ps1
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-530 — Add a -Pop3 session mode to Record-CurlExchange.ps1

## Goal

`Record-CurlExchange.ps1 -Pop3` serves one POP3 session the way `-Ftp` serves FTP: a greeting, one reply per command from a default table overridable per verb, multi-line replies ended by `.` with dot-stuffing, `STLS` switching to TLS, and every line recorded in `request.bin` and `transcript.txt`.

## Context

- Needed to measure `pop3://`/`pop3s://` (audit row 34) before any POP3 bytes are pinned.
- Model on `-Ftp` (`.PARAMETER Ftp`, `.PARAMETER FtpReply`, `-Tls`).
- Default replies (RFC 1939, RFC 2449, RFC 5034): greeting `+OK POP3 ready <1896.697170952@localhost>` (the APOP timestamp), `CAPA` a multi-line list with `USER`, `SASL PLAIN LOGIN`, `STLS`, `TOP`, `UIDL`; `USER +OK`; `PASS +OK`; `APOP +OK`; `AUTH` `+` continuations then `+OK`; `STAT +OK 2 20`; `LIST` multi-line two messages (and `LIST n` single-line); `RETR n` multi-line from a `-Pop3Message` text with a line starting `.` to exercise stuffing; `DELE +OK`; `TOP` multi-line; `UIDL` multi-line; `NOOP +OK`; `QUIT +OK`; anything else `-ERR`.

## Acceptance criteria

- [x] `.PARAMETER Pop3`, `.PARAMETER Pop3Reply` and `.PARAMETER Pop3Message` are documented in the script header.
- [x] `Record-CurlExchange.ps1 -Pop3 -CurlArgs '-sS','-u','u:p','pop3://127.0.0.1:<P>/1'` against the reference curl writes the commands curl sent and the message it printed.
- [x] A `-Pop3Reply 'PASS=-ERR denied'` override takes effect; `STLS` with `--ssl-reqd -k` completes over TLS.
- [x] The existing HTTP and `-Ftp` modes produce identical files before and after the change.

## Notes

- Built as `$servePop3Session`, modelled on `-Smtp`'s session (same Send-Reply/Read-Line/Start-Tls shape, shared `Get-Override` and `Wrap-Tls` helpers), plus `-Pop3IdleMilliseconds` (default 5000) like the other line modes. `-Pop3 -Tls` serves implicit POP3S for `pop3s://`.
- Choice: the maildrop is two copies of `Pop3Message`, so `STAT` answers `+OK 2 <2 x its size>` (266 with the default message) rather than the task's literal `+OK 2 20`; `LIST`, `STAT` and `RETR` then agree with each other, which a real server's would.
- Choice: `AUTH` with no mechanism answers a multi-line PLAIN/LOGIN list (RFC 5034 4); `LIST n`/`UIDL n` answer any `n` without checking it exists; `TOP n k` sends the headers, the blank line and `k` body lines.
- Learned from the reference curl 8.21.0: with the default greeting's `<...>` timestamp and a CAPA without SASL, curl logs in with `APOP`, not `USER`/`PASS`. To exercise `PASS` override both `GREETING` (no timestamp) and `CAPA` (no SASL): `-Pop3Reply 'PASS=-ERR denied','CAPA=+OK\r\nUSER\r\n.','GREETING=+OK ready'` gives `USER u`, `PASS p`, `-ERR denied`, and curl exits 67 with `curl: (67) Access denied. -`. With the default table curl uses `AUTH PLAIN` then a `+ ` continuation.
- Verified: `pop3://.../1` records CAPA, AUTH PLAIN, RETR 1 (the `.A line` sent stuffed as `..A line`), QUIT, and stdout holds the unstuffed message; `--ssl-reqd -k` records STLS, the handshake line, CAPA without STLS, AUTH, LIST; `-Tls` with `-X 'TOP 1 0'` over `pop3s://` works. HTTP and `-Ftp` fixtures before and after are byte-identical (the EPSV port, random per run, masked).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Record-CurlExchange.ps1 -Pop3 serves one POP3/POP3S session with overridable replies, dot-stuffed multi-line replies and STLS
