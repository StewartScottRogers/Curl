---
id: BL-554
title: Authenticate an IMAP session with LOGIN or AUTHENTICATE
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-553, BL-536]
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-554 — Authenticate an IMAP session with LOGIN or AUTHENTICATE

## Goal

The IMAP handler logs in as curl 8.21.0 does: `AUTHENTICATE <mech>` through the injected SASL authenticator when `AUTH=` capabilities are offered (with `SASL-IR` and `--sasl-ir`), else `LOGIN` with curl's quoting of user and password, honouring `--login-options`, and a `NO` mapped to exit 67 and curl's message.

## Context

- Conformance audit 2026-09-28, rows 23 and 34. SASL: BL-533's ADR, BL-534, BL-536.
- Measure with `Record-CurlExchange.ps1 -Imap`: `-u u:p` with `AUTH=PLAIN` offered, with none offered (`LOGIN`), a password containing `"` and `\` and a space, `--login-options AUTH=LOGIN`, `SASL-IR` advertised with `--sasl-ir`, and `LOGIN` answered `NO`.

## Acceptance criteria

- [ ] Measured first as above; request lines, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Imap.UnitTests` pin client bytes (quoting byte for byte) and outcome for each case.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
