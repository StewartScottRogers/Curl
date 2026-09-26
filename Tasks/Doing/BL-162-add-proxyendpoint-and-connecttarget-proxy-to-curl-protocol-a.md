---
id: BL-162
title: Add ProxyEndpoint and ConnectTarget.Proxy to Curl.Protocol.Abstractions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-157]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-162 — Add ProxyEndpoint and ConnectTarget.Proxy to Curl.Protocol.Abstractions

## Goal

`ProxyEndpoint(ProxyKind, Host, Port, NetworkCredential?)` and an optional `ConnectTarget.Proxy` exist as the BL-157 ADR states.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item X6. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- `ConnectTarget` (`Curl.Protocol.Abstractions.UnitLibrary/ConnectTarget.cs`) validates Host and Port in its property initialisers (`RequireHost`, `RequirePort`); `ProxyEndpoint` follows the same pattern.
- BL-212 (CONNECT), BL-213 (SOCKS), BL-206 (proxy selection) and BL-183 (forward proxy) depend on this.

## Acceptance criteria

- [ ] `ProxyEndpoint` rejects an empty host and a port outside 1-65535 with the same exceptions `ConnectTarget` throws; tests cover each.
- [ ] A test shows a `with` expression on `ProxyEndpoint` and on `ConnectTarget` cannot bypass the checks.
- [ ] `ConnectTarget.Proxy` defaults to null and existing callers compile unchanged.
- [ ] `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Abstractions`.

## Notes

- Plan item: X6 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
