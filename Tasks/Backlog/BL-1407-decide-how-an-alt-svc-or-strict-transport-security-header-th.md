---
id: BL-1407
title: Decide how an Alt-Svc or Strict-Transport-Security header the caches ignore is reported as curl's -v line before that header line
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-10-03
completed:
---
# BL-1407 — Decide how an Alt-Svc or Strict-Transport-Security header the caches ignore is reported as curl's -v line before that header line

## Goal

An ADR, "Decided by Claude under Stewart's delegation", fixes how Curl writes curl 8.21.0's `-v` lines for an `Alt-Svc` alternative the alt-svc cache skips and an `Strict-Transport-Security` header the HSTS cache refuses, in their place just before the header line, and files the implementation tasks in dependency order.

## Context

- Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Tls`, `-sv -k --resolve h.test:PORT:127.0.0.1 --hsts <file> --alt-svc <file> https://h.test:PORT/a`, reply headers `Strict-Transport-Security: max-age=abc` then `Alt-Svc: <value>`:
  - `* Illegal STS header skipped` is written just before `< Strict-Transport-Security: max-age=abc` (for an IP-address host no line is written).
  - `Alt-Svc: h2=":abc"`, `h2="[::1]:99999"` and `h2="host:"` each write `* Unknown alt-svc port number, ignoring.` just before the `< Alt-Svc:` line; `h2="[::1:443"` writes `* Bad alt-svc IPv6 hostname, ignoring.`; over plain `http://` nothing is written.
- curl 8.21.0 (tag `curl-8_21_0`): `lib/http.c` lines 3550-3573 (`infof(data, "Illegal STS header skipped")` when `Curl_hsts_parse` fails); `lib/altsvc.c` lines 515-545 (`Bad alt-svc hostname, ignoring.` for a host over the length limit, `Bad alt-svc IPv6 hostname, ignoring.`, `Unknown alt-svc port number, ignoring.`).
- Today none of these texts exists in the solution. The HTTP handler reports `Added alt-svc: ...` before the header line through `IAltSvcStore.StoreFromResponse` (`Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` `StoreAltSvc`, around line 1785; the interface is in `Curl.Protocol.Abstractions.UnitLibrary/IAltSvcStore.cs`), which returns only the alternatives added; `Curl.Core.UnitLibrary/AltSvc/AltSvcHeaderParser.cs` stops reading at a bad host or port without saying why. HSTS headers are learned after the transfer from the report (`Curl.Core.UnitLibrary/Hsts/HstsTransferPolicy.cs` `LearnFrom`), so there is no point during header reading at which a line could be written today.
- The decision must say which contract changes (for example a reason on the parser's result and on `IAltSvcStore`, and an HSTS seam the HTTP handler calls per header) and keep `Curl.Protocol.Abstractions.UnitLibrary` changes small, since they run apart from every protocol task.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/` marked "Decided by Claude under Stewart's delegation" names the contract changes, the projects each touches (all existing in `Curl.slnx`), the measured lines above as the behaviour to pin, and why the design was chosen over writing the lines after the transfer.
- [ ] The implementation tasks are filed with `task-board.ps1 new` in dependency order (contract first, then Core, then the HTTP handler and Curl.Console wiring), each with exact `touches` and checkable criteria that pin the measured `-v` sequences.
- [ ] `Documentation/Planning/Decisions/README.md` lists the ADR if it keeps an index.

## Notes

- No code changes in this task.

## Log

- 2026-10-03: Created.
