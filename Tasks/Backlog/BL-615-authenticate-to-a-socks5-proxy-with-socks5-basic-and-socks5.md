---
id: BL-615
title: Authenticate to a SOCKS5 proxy with --socks5-basic and --socks5-gssapi
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-612, BL-527, BL-691]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-615 — Authenticate to a SOCKS5 proxy with --socks5-basic and --socks5-gssapi

## Goal

The SOCKS5 greeting offers the methods `--socks5-basic` and `--socks5-gssapi` select (as curl 8.21.0 offers them, including its default), username/password authentication (RFC 1929) works with the proxy credentials, and GSS-API authentication (RFC 1961, with `--socks5-gssapi-service` and `--socks5-gssapi-nec`) works through the Kerberos GSS-API mechanism (BL-691) behind the seam of BL-525 and BL-527, including RFC 1961's per-message protection negotiation, on every platform.

## Context

- Conformance audit 2026-09-28, row 16 (Major). Options: BL-612; seam: BL-525, BL-527; GSS Wrap/Unwrap for the protection-level exchange: BL-691. Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): SOCKS5 GSS-API works on every platform, no refusal.
- Code: `Curl.Networking.UnitLibrary/Socks5Handshake.cs` (ADR-0084 follows the Schannel build).

## Acceptance criteria

- [ ] Measured first with a scripted SOCKS exchange (as in BL-614): the method list curl offers with neither option, with `--socks5-basic` only and with `--socks5-gssapi` only, and a refused username/password; request bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Networking.UnitTests` pin each greeting, the RFC 1929 exchange, the GSS-API exchange with a fake token source, and each failure.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
