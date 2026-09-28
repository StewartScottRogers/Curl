---
id: BL-547
title: Open a POP3 session: greeting, CAPA, STLS, pop3s and QUIT
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-534, BL-530]
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-547 — Open a POP3 session: greeting, CAPA, STLS, pop3s and QUIT

## Goal

A `Pop3ProtocolHandler` in `Curl.Protocol.Pop3.UnitLibrary` connects through `IConnector`, reads the greeting (keeping the APOP timestamp), sends `CAPA`, upgrades with `STLS` per `--ssl`/`--ssl-reqd` or starts in TLS for `pop3s://`, ends with `QUIT`, and maps every failure to curl 8.21.0's exit code and message.

## Context

- Conformance audit 2026-09-28, row 34 (Blocker, L; POP3 split: this session, BL-548 auth, BL-549 list and retrieve, BL-550 custom commands and options, BL-551 registration, BL-552 `-v`).
- Design: BL-533's ADR; contract: BL-534; timeouts: BL-498's ADR; endpoints: BL-515's ADR. `Curl.Protocol.Pop3.UnitLibrary/CLAUDE.md` rules apply (Abstractions only, `IConnection`, no `Socket`/`SslStream`).
- Measure with `Record-CurlExchange.ps1 -Pop3` (BL-530): the default session, a `-ERR` greeting, `CAPA` answered `-ERR`, `STLS` refused under `--ssl` and `--ssl-reqd`, `pop3s://` with `-Tls -k`, a malformed reply, and the server closing early.

## Acceptance criteria

- [ ] Measured first as above; request lines, stdout, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Pop3.UnitTests` drive the handler through a fake `IConnector`/`IConnection` replaying the measured bytes and pin client bytes and outcome for each case, including multi-line `CAPA` split across reads.
- [ ] No test needs `TestCategory=Integration`; tests are platform-neutral.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Pop3.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
