---
id: BL-4213
title: OAuth2 extension: send a token request over IConnection with its own small HTTP/1.1 client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4212]
touches: [Curl.Extension.OAuth2.UnitLibrary/Http, Curl.Extension.OAuth2.UnitTests/Http]
requirement: none
created: 2026-10-29
completed:
---
# BL-4213 — OAuth2 extension: send a token request over IConnection with its own small HTTP/1.1 client

## Goal

The OAuth2 extension POSTs a form body to a token or device authorization endpoint over an `IConnection` with HTTP/1.1 and reads the JSON response, with no network in its tests.

## Context

Second part of BL-4161 (split for size). An extension may not reference `Curl.Protocol.Http` or another extension (ADR-0999), so, like `Curl.Extension.S3.UnitLibrary/Http` (ADR-1018), write a small HTTP/1.1 client in `Http/`: one request, `Content-Type: application/x-www-form-urlencoded`, `Accept: application/json`, the response framed by `Content-Length`, chunked or the close (RFC 9112), status line and headers read; a 400 or 401 body is still returned so the error response (RFC 6749 §5.2) can be read. TLS comes from `ConnectTarget.UseTls` through the connector. Every failure is a typed failure, never an unhandled exception.

## Acceptance criteria

- [ ] Unit tests over fake connections or recorded bytes check the request bytes and each response framing (RFC 9112 §6, §7.1).
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records that the extension carries its own client and why, or the task cites ADR-1018 under Notes if it already covers it.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4161's lane 2 run (split for size).

## Log

- 2026-10-29: Created.
