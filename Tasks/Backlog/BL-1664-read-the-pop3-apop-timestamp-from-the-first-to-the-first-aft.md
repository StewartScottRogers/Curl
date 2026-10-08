---
id: BL-1664
title: Read the POP3 APOP timestamp from the first < to the first > after it, as curl does
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1664 — Read the POP3 APOP timestamp from the first < to the first > after it, as curl does

## Goal

A POP3 greeting whose timestamp is followed by more text (`+OK hi <a@b> trailing`, `+OK <a@b> <c@d>`, `+OK <a@b>  `) logs in with `APOP` over the digest of `<a@b>`, as curl 8.21.0 does, instead of falling back to `USER`/`PASS` or sending the digest of the wrong string.

## Context

- Found by BL-1514's adversarial tests. `Pop3ApopTimestamp.Read` (`Curl.Protocol.Pop3.UnitLibrary/Pop3ApopTimestamp.cs`) needs the whole greeting to end in `>` and takes everything from the first `<` to the end of the line.
- Measured 2026-10-07 with `Record-CurlExchange.ps1 -Pop3 -Pop3Reply "GREETING=<g>","CAPA=+OK\r\nUSER\r\n." -CurlArgs '-sS','-u','u:p','pop3://127.0.0.1:18114/'` (Schannel build): curl takes the timestamp from the first `<` to the first `>` after it, and uses APOP when an `@` lies between them.
  - `+OK hi <a@b> trailing` -> `APOP u e448b4da727938a4e29f25d416b01191` (MD5 of `<a@b>p`); Curl sends `USER u` / `PASS p` - the password in the clear where curl sends a digest, hence High.
  - `+OK <a@b> <c@d>` -> `APOP u e448b4da727938a4e29f25d416b01191`; Curl digests `<a@b> <c@d>p` (`9056564dbb8607a80ebb2796a9a0e2af`), so the server refuses it.
  - `+OK <a@b>  ` (trailing spaces) -> `APOP u e448b4da727938a4e29f25d416b01191`; Curl sends `USER`/`PASS`.
  - Already matching, pinned in `Pop3ProtocolHandlerAdversarialTests.ExecuteAsync_MalformedApopTimestamp_LogsInAsCurlDoes`: `<@>`, `+OK<a@b>`, `<nohost>`, `<a@b`, `a@b>`.

## Acceptance criteria

- [ ] `ExecuteAsync_MalformedApopTimestamp_LogsInAsCurlDoes` in `Curl.Protocol.Pop3.UnitTests` gains the three greetings above, each expecting `APOP u e448b4da727938a4e29f25d416b01191`, and passes.
- [ ] The existing login tests still pass, and `Curl.Protocol.Pop3.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-10-07: Created.
