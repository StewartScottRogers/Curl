---
id: BL-4213
title: OAuth2 extension: send a token request over IConnection with its own small HTTP/1.1 client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4212]
touches: [Curl.Extension.OAuth2.UnitLibrary/Http, Curl.Extension.OAuth2.UnitTests/Http, Curl.Extension.OAuth2.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4213 — OAuth2 extension: send a token request over IConnection with its own small HTTP/1.1 client

## Goal

The OAuth2 extension POSTs a form body to a token or device authorization endpoint over an `IConnection` with HTTP/1.1 and reads the JSON response, with no network in its tests.

## Context

Second part of BL-4161 (split for size). An extension may not reference `Curl.Protocol.Http` or another extension (ADR-0999), so, like `Curl.Extension.S3.UnitLibrary/Http` (ADR-1018), write a small HTTP/1.1 client in `Http/`: one request, `Content-Type: application/x-www-form-urlencoded`, `Accept: application/json`, the response framed by `Content-Length`, chunked or the close (RFC 9112), status line and headers read; a 400 or 401 body is still returned so the error response (RFC 6749 §5.2) can be read. TLS comes from `ConnectTarget.UseTls` through the connector. Every failure is a typed failure, never an unhandled exception.

## Acceptance criteria

- [x] Unit tests over fake connections or recorded bytes check the request bytes and each response framing (RFC 9112 §6, §7.1).
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records that the extension carries its own client and why, or the task cites ADR-1018 under Notes if it already covers it.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4161's lane 2 run (split for size).
- Plan: Http/ holds OAuth2HttpClient (connects through IConnector, TLS from ConnectTarget.UseTls), OAuth2HttpRequestWriter, OAuth2HttpExchange and a reader, head parser and body reader adapted from the S3 extension's client (ADR-1018), reading the whole body (at most 1 MiB) whatever the status. ADR-1030 records why the extension carries its own client and the exit code each failure maps to.
- Default taken: Connection: close on every request, since a token exchange is one request; interim 1xx responses are skipped.
- touches widened to Curl.Extension.OAuth2.UnitLibrary/CLAUDE.md so the project doc describes Http/; no task in Doing on origin/work/dark-factory names it.
- Measured: Measure-CodeQuality.ps1 -Library Curl.Extension.OAuth2.UnitLibrary gives 100% line, 100% branch, 0 failing members, worst CRAP 10. Solution build clean; fast tests 46652 passed, 0 failed. Tests are platform-neutral (no paths, OS text or certificates), so CI on Linux and macOS is expected green.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 1cd6a8b9 2c9055fd 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. The OAuth2 extension POSTs a form to a token endpoint over IConnector with its own HTTP/1.1 client and reads the whole response or a typed failure
