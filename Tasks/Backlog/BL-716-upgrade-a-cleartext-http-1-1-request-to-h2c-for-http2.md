---
id: BL-716
title: Upgrade a cleartext HTTP/1.1 request to h2c for --http2
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-659]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-716 — Upgrade a cleartext HTTP/1.1 request to h2c for --http2

## Goal

`curl --http2 http://host/` sends curl 8.21.0's HTTP/1.1 upgrade request (`Connection: Upgrade, HTTP2-Settings`, `Upgrade: h2c`, `HTTP2-Settings: <base64url SETTINGS>`), switches to HTTP/2 on `101 Switching Protocols` (the response arriving on stream 1) and carries on over HTTP/1.1 when the server ignores the upgrade, on every platform.

## Context

- Conformance audit 2026-09-28, row 32; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-659 (HTTP/2 path, option acceptance) and BL-658 (request and response on a stream). RFC 7540 section 3.2 (the h2c upgrade; RFC 9113 deprecates it but curl still sends it for `--http2` over `http://`: confirm by measurement).
- Measure with an OpenSSL build of curl through `Record-CurlExchange.ps1`: `--http2 -v http://127.0.0.1:<P>/` against the plain HTTP/1.1 server (the upgrade headers curl sends, and what it does when ignored), and against an h2c-capable server through `-NoServer` if one is available (for example `nghttpd --no-tls`).

## Acceptance criteria

- [ ] Measured first as above; request bytes and stderr copied into Notes.
- [ ] `Curl.Protocol.Http.UnitTests` pin the upgrade request bytes, the switch to HTTP/2 after a `101` with the response read from stream 1, and the unchanged HTTP/1.1 path when the server answers `200` without upgrading, through a fake connection.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
