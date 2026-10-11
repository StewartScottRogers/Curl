---
id: BL-2085
title: Honour Proxy-Connection: close in HttpConnectionPersistence.KeepsAlive (upstream test258)
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-11
completed:
---
# BL-2085 — Honour Proxy-Connection: close in HttpConnectionPersistence.KeepsAlive (upstream test258)

## Goal

Curl does not reuse a connection after a response carrying `Proxy-Connection: close`, so upstream test258 passes instead of Curl exiting 52.

## Context

Upstream test258 gets a 407 carrying `Proxy-Connection: close` and expects the retried request on a new connection. `HttpConnectionPersistence.KeepsAlive` (Curl.Protocol.Http.UnitLibrary/HttpConnectionPersistence.cs, near line 38) ignores that header, so Curl writes the retry to the closed connection and fails with exit 52. Read 8.21.0 `tests/data/test258` and measure real curl with `Record-CurlExchange.ps1` first; `ProxyConnectionNames` already exists in the same file for another purpose. The case is item `behaviour:test258` of GF-0005.

Source: re-measurement of the gap findings with the fixed gap harness (PR #119, 2026-10-11); parent re-close task BL-2074. Targeted curl version: 8.21.0 (tests/data in the release tarball).

## Acceptance criteria

- [ ] A new MSTest in Curl.Protocol.Http.UnitTests (no network) shows `KeepsAlive` is false for a response with `Proxy-Connection: close` on HTTP/1.1 and HTTP/1.0, in any letter case, and unchanged for `keep-alive`, as curl 8.21.0 behaves.
- [ ] A handler-level test shows the 407 retry in test258's shape opens a second connection and the transfer ends with the exit code the case expects.
- [ ] Add test258 to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` once it passes in process.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

## Log

- 2026-10-11: Created.
