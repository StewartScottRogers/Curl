---
id: BL-548
title: Authenticate a POP3 session with USER and PASS, APOP or SASL AUTH
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-547, BL-536]
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-548 — Authenticate a POP3 session with USER and PASS, APOP or SASL AUTH

## Goal

The POP3 handler logs in as curl 8.21.0 does: SASL `AUTH` through the injected authenticator when `CAPA` offers SASL, else `APOP` when the greeting carries a timestamp, else `USER`/`PASS`, with `--login-options AUTH=+APOP`/`AUTH=<mech>` steering the choice, and a refusal mapped to exit 67 and curl's message.

## Context

- Conformance audit 2026-09-28, rows 23 and 34. SASL: BL-533's ADR, BL-534, BL-536. APOP is MD5 of timestamp and password (RFC 1939; `System.Security.Cryptography.MD5`).
- Measure with `Record-CurlExchange.ps1 -Pop3`: `-u u:p` with SASL offered, with only `USER` offered, with an APOP timestamp and no SASL, with `--login-options AUTH=+APOP`, and with `PASS` answered `-ERR`.

## Acceptance criteria

- [ ] Measured first as above; request lines, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Pop3.UnitTests` pin the client bytes (the APOP digest byte for byte) and outcome for each case.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Pop3.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
