---
id: BL-712
title: Authenticate with TLS-SRP for --tlsuser, --tlspassword and --tlsauthtype on every platform
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-618, BL-704, BL-708]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-712 — Authenticate with TLS-SRP for --tlsuser, --tlspassword and --tlsauthtype on every platform

## Goal

`--tlsuser u --tlspassword p` (with `--tlsauthtype SRP`, the only type and the default) authenticates the TLS connection with SRP on every platform, and `--proxy-tlsuser`, `--proxy-tlspassword` and `--proxy-tlsauthtype` do the same for an HTTPS proxy, with curl 8.21.0's messages and exit codes for a wrong password and a server without SRP.

## Context

- Conformance audit 2026-09-28, row 18; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): curl's OpenSSL and GnuTLS builds offer TLS-SRP, so Curl does everywhere. Parsing: BL-618 (and the proxy forms: BL-605 parses the HTTPS-proxy TLS options; if the three proxy SRP options are not among them, parse them here). Routing: BL-617's ADR and BL-708; the exchange: BL-704.
- Measure with an OpenSSL build of curl against `openssl s_server -srpvfile` through `Record-CurlExchange.ps1 -NoServer`: right and wrong password, and a server without SRP; stderr and exit code copied into Notes.

## Acceptance criteria

- [ ] Measured first as above; copied into Notes.
- [ ] Tests pin a successful SRP transfer against the in-memory SRP server, the measured failures, and the proxy options reaching only the proxy's handshake.
- [ ] `curl -V` lists `TLS-SRP` among the features (ADR-0021), with a test.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
