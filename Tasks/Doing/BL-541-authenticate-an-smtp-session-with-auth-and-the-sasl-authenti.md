---
id: BL-541
title: Authenticate an SMTP session with AUTH and the SASL authenticator
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-540, BL-536]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-541 — Authenticate an SMTP session with AUTH and the SASL authenticator

## Goal

With `-u` (or `--oauth2-bearer`), the SMTP handler authenticates after `EHLO` (and after `STARTTLS`) with `AUTH <mech>` through the injected SASL authenticator, following `334` continuations, and fails a `535` or other refusal with curl 8.21.0's exit 67 and message.

## Context

- Conformance audit 2026-09-28, rows 23 and 34. SASL contract and choice: BL-533's ADR, BL-534, BL-536.
- Measure with `Record-CurlExchange.ps1 -Smtp`: `-u u:p` with `AUTH PLAIN LOGIN` offered, with `--sasl-ir`, with `--login-options AUTH=LOGIN`, with the server answering `535`, with no `AUTH` advertised but `-u` given, and with `--oauth2-bearer`.

## Acceptance criteria

- [ ] Measured first as above; request lines, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Smtp.UnitTests` pin each case's client bytes and outcome through a fake connection and a fake SASL authenticator where the mechanism's bytes are not the point.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
