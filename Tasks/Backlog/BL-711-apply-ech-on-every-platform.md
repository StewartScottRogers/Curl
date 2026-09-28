---
id: BL-711
title: Apply --ech on every platform
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-618, BL-706, BL-707, BL-708]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-711 — Apply --ech on every platform

## Goal

`--ech false|grease|true|hard|ecl:<b64>|pn:<name>` behaves as curl 8.21.0 does on every platform: GREASE, opportunistic and mandatory ECH with the configuration from the option or from DoH, and curl's exit code and message when `hard` cannot use ECH.

## Context

- Conformance audit 2026-09-28, row 18; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Parsing: BL-618; routing: BL-617's ADR and BL-708; ECH: BL-706; configurations from DoH: BL-707. Mode semantics: curl's `docs/ECH.md` and `docs/cmdline-opts/ech.md` at tag `curl-8_21_0`.
- Measure with a curl build that has ECH through `Record-CurlExchange.ps1 -Tls -k` (and a DoH responder): each mode against a server without ECH; stderr and exit code copied into Notes.

## Acceptance criteria

- [ ] Measured first as above; copied into Notes.
- [ ] Tests pin each mode's ClientHello (ECH or GREASE extension present or absent), the configuration source used, and the `hard` failure's exit code and message.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
