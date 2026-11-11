---
id: BL-4940
title: Show the webtransport:// URL form in --extended-help and Protocols.md
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Extended.Protocol.WebTransport.UnitLibrary/WebTransportExtension.cs, Documentation/Protocols.md]
requirement: none
created: 2026-11-11
completed:
---
# BL-4940 — Show the webtransport:// URL form in --extended-help and Protocols.md

## Goal

The webtransport entry names the URL a user types, `webtransport://host[:port]/path[?wt=...]`.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-018 (Low, confirmed: reproduced at least twice).

- Repro: `curl.exe --extended-help | grep ^webtransport`
- Expected: Like every other scheme entry, the typed URL form.
- Actual: Says "...at https://host[:port]/path (UDP port 443)..." and never mentions `webtransport://`; a user typing `https://` gets plain HTTP.
- Where to start: `WebTransportExtension.cs` line 12; the `Documentation/Protocols.md` row.

## Acceptance criteria

- [ ] `curl.exe --extended-help | grep ^webtransport | grep -c "webtransport://host"` prints 1.
- [ ] The Protocols.md row shows the same form.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: da28a5b3 41cae4e9 a0b49a29.
