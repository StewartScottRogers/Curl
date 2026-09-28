---
id: BL-604
title: Answer proxy NTLM and Negotiate challenges for tunnels and forward proxies
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-602, BL-603, BL-526, BL-527]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-604 — Answer proxy NTLM and Negotiate challenges for tunnels and forward proxies

## Goal

`--proxy-ntlm` and `--proxy-negotiate` (and `--proxy-anyauth` when they rank first) complete their handshakes against a `407` on the same proxy connection, for `CONNECT` tunnels and forward proxy requests, using the NTLM and Negotiate implementations from BL-526 and BL-527, as curl 8.21.0 does.

## Context

- Conformance audit 2026-09-28, row 14 (Major). Builds on BL-602 (tunnel), BL-603 (forward) and BL-525's ADR (the token seam).
- NTLM authenticates the connection, so the tunnel's retry must stay on one connection.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` (`-HoldOpenMilliseconds` so the retry reuses the connection) as the proxy: `--proxy-ntlm -U u:p` for a tunnel and for a forward request with a fixed Type 2 challenge; request bytes, stderr and exit code copied into Notes.
- [ ] Tests pin the three-leg exchange for both paths with the seam's fixed inputs, and the Negotiate header with a fake token source.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
