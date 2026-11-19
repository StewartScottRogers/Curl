---
id: BL-5496
title: Fix fitness finding EXT-102: STOMP CONNECT copies a line feed from --user or -H host: into the frame, adding headers that override login
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompProtocolHandler.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5496 — Fix fitness finding EXT-102: STOMP CONNECT copies a line feed from --user or -H host: into the frame, adding headers that override login

## Goal

EXT-102 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: STOMP CONNECT copies a line feed from --user or -H host: into the frame, adding headers that override login. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low (needs the user's own command-line input; URL user info with `%0A` is already refused with exit 3)
- **Confidence:** Confirmed (twice, against a frame-logging mock and against RabbitMQ)
- **Protocol/area:** stomp, stomps
- **Repro:** a TCP mock on 127.0.0.1:46901 that logs each NUL-terminated frame, answers CONNECT with `CONNECTED\nversion:1.2\n\n\0`, and answers a frame carrying `receipt:` with a RECEIPT.
  - `curl -sS -m 3 -u $'rguest\npasscode:forged:ignored' -d x stomp://127.0.0.1:46901/queue/a`
  - `curl -sS -m 3 -H $'host: /\nlogin:x' -u rguest:rpass -d x stomp://127.0.0.1:46901/queue/a`
  - Against RabbitMQ 4 with the STOMP plugin on 46613 (user `rguest:rpass`): `curl -sS -m 5 -u rguest:rpass -H $'host: /\nlogin:x' -d x stomp://127.0.0.1:46613/queue/sq1`
- **Expected:** STOMP 1.2 "Value Encoding": CONNECT/CONNECTED headers are not escaped, so a value with LF or CR cannot be sent. The port already escapes the SEND destination and refuses control characters in URL user info. Refuse such a value before connecting (exit 2 or 3) with a message naming the field.
- **Actual (PORT):** the mock logs `CONNECT\naccept-version:1.2\nhost:/\nheart-beat:0,0\nlogin:rguest\npasscode\npasscode:forged:ignored\n\n` and `CONNECT\naccept-version:1.2\nhost:/\nlogin:x\nheart-beat:0,0\nlogin:rguest\npasscode:rpass\n\n`. RabbitMQ answers `curl: (67) STOMP server sent ERROR: Bad CONNECT: Access refused for user 'x'` (the injected `login` wins).
- **Likely location:** `Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompProtocolHandler.cs` (CONNECT header building for `login`, `passcode`, `host`).
- **Acceptance check:** both mock commands exit non-zero before connecting with a message such as `STOMP login holds a line break`, and the mock logs no frame.

## Acceptance criteria

- [x] both mock commands exit non-zero before connecting with a message such as `STOMP login holds a line break`, and the mock logs no frame.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Line feed or carriage return in login, passcode or host is refused before connecting with exit 3 (UrlMalformat, as for URL user info), message "STOMP <field> holds a line break". Quality run: 0 failing members.

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding EXT-102 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 16f12f57 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. Refuse line breaks in CONNECT login, passcode, host
