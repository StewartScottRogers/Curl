---
id: BL-707
title: Decode DNS HTTPS records and fetch a host's ECH configuration through DoH
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-641]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-707 — Decode DNS HTTPS records and fetch a host's ECH configuration through DoH

## Goal

The DNS codec in `Curl.Networking.UnitLibrary` encodes HTTPS (type 65) queries and decodes HTTPS/SVCB records (RFC 9460: priority, target, `alpn`, `port`, `ipv4hint`, `ipv6hint`, `ech`), and the DoH resolver can fetch a host's HTTPS record, so `--ech true`/`hard` finds the ECHConfigList through DoH as curl does.

## Context

- curl obtains ECH configurations from HTTPS records via DoH (curl's `docs/ECH.md` at tag `curl-8_21_0` describes it; read it and record the exact behaviour in Notes). Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28).
- Builds on BL-640 (codec) and BL-641 (DoH resolver). Record the HTTPS query curl sends through `Record-CurlExchange.ps1 -Tls -k` as a DoH server with a curl build that has ECH.

## Acceptance criteria

- [ ] Measured first as above; the DoH query bytes copied into Notes.
- [ ] `Curl.Networking.UnitTests` pin the HTTPS query bytes, decode RFC 9460 Appendix D's example records and an answer with an `ech` parameter, and reject a malformed SvcParam list with a typed failure.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
