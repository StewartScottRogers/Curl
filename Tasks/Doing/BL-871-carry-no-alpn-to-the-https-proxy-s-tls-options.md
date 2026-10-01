---
id: BL-871
title: Carry --no-alpn to the HTTPS proxy's TLS options
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-753]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-871 — Carry --no-alpn to the HTTPS proxy's TLS options

## Goal

`--no-alpn` removes the ALPN offer from the TLS handshake with an HTTPS proxy (tunnel and forward), so `curl -v --no-alpn --proxy-insecure -x https://... http://...` prints no `ALPN:` line for the proxy, as curl 8.21.0 does on both builds.

## Context

- ADR-0190 / BL-753: the proxy handshake now offers `http/1.1`; both builds offer nothing under `--no-alpn` (measured, lines in BL-753's Notes).
- The providers already drop the offer when `TlsClientOptions.UseAlpn` is off; `Curl.Console/TlsClientOptionsMapping.cs` `ProxyFromCommandLine` leaves it at its default (`true`), so `--no-alpn` never reaches the proxy's provider.
- Fix: pass `UseAlpn: options.UseAlpn` in `ProxyFromCommandLine` and update its doc comment.

## Acceptance criteria

- [ ] A test in `Curl.Console.UnitTests/TlsClientOptionsMappingTests.cs` pins that `ProxyFromCommandLine` of a command line with `--no-alpn` has `UseAlpn` false, and without it true.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports no failing member in `Curl.Console`.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
