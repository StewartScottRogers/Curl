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
completed: 2026-09-26
---
# BL-162 — Add ProxyEndpoint and ConnectTarget.Proxy to Curl.Protocol.Abstractions

## Goal

`ProxyEndpoint(ProxyKind, Host, Port, NetworkCredential?)` and an optional `ConnectTarget.Proxy` exist as the BL-157 ADR states.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item X6. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- `ConnectTarget` (`Curl.Protocol.Abstractions.UnitLibrary/ConnectTarget.cs`) validates Host and Port in its property initialisers (`RequireHost`, `RequirePort`); `ProxyEndpoint` follows the same pattern.
- BL-212 (CONNECT), BL-213 (SOCKS), BL-206 (proxy selection) and BL-183 (forward proxy) depend on this.

## Acceptance criteria

- [x] `ProxyEndpoint` rejects an empty host and a port outside 1-65535 with the same exceptions `ConnectTarget` throws; tests cover each.
- [x] A test shows a `with` expression on `ProxyEndpoint` and on `ConnectTarget` cannot bypass the checks.
- [x] `ConnectTarget.Proxy` defaults to null and existing callers compile unchanged.
- [x] `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Abstractions`.

## Notes

- Plan item: X6 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- `ProxyEndpoint` and `ProxyKind` were already in the library: they landed with
  `HttpRequestOptions.ForwardProxy` (commit 5f4d607), which needed the type. This run
  added only `ConnectTarget.Proxy` (`ProxyEndpoint?`, `init`, default null) and the tests.
  Delivered directly rather than through the full `/feature` agent stages: the shape is
  fixed verbatim by ADR-0014, so there was nothing left to plan (default taken, unattended run).
- The "`with` cannot bypass the checks" tests assert that `Host` and `Port` have no setter
  by reflection. A test cannot otherwise show a non-compiling `with { Host = "" }`.
- `Measure-CodeQuality.ps1`: Curl.Protocol.Abstractions.UnitLibrary 100% line, 100% branch,
  261 members, 0 failing, worst CRAP 1. The script exits non-zero only because of three
  Curl.Networking.UnitLibrary members, which predate this task and sit outside its `touches`.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. ConnectTarget.Proxy (ProxyEndpoint?, default null) exists beside ProxyEndpoint; with-expression bypass is tested for both
