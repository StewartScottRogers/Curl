---
id: BL-2719
title: Keep user:password and HTTP/1.0 downgrade in an FTP URL sent through an HTTP proxy (upstream cases 208, 299 and 1077)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2719 — Keep user:password and HTTP/1.0 downgrade in an FTP URL sent through an HTTP proxy (upstream cases 208, 299 and 1077)

## Goal

Upstream cases 208, 299 and 1077 pass and are on the ratchet.

## Context

Filed by BL-2610. 208 and 299 expect `ftp://user:pass@host/...` kept in the proxied request line (Curl sends `ftp://host/...`); 1077 expects `HTTP/1.0` after a proxy downgrade (Curl sends HTTP/1.1). Run with `CURL_UPSTREAM_CASE=208` etc.

## Acceptance criteria

- [x] The cases pass through the runner and are added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, each fix with a focused unit test in the fixed library's `.UnitTests` (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- Run directly rather than through the full `/feature` stages: both fixes are small, local to `Curl.Protocol.Http.UnitLibrary`, and the upstream cases pin the bytes.
- 1077: the proxied FTP connection was shut down after the first HTTP/1.0 response, so the second URL opened a new one and went out as HTTP/1.1. curl's `http.c` keeps an HTTP/1.0 connection through an HTTP proxy alive on `Proxy-Connection: keep-alive`; `HttpConnectionPersistence.KeepsAlive` now does too, and the existing `Http1VersionSeen` downgrade then sends HTTP/1.0 on the reused connection.
- 208, 299: curl's `http_target` keeps the user and password in a proxied `ftp` URL only, and `-u` lands in the URL too. `HttpRequestHeadFormatter` now takes the transfer's credentials and writes `user:password@` into an `ftp` absolute-form target, each percent-encoded with `Uri.EscapeDataString` (curl's `CURLU_URLENCODE` keeps only unreserved characters), with no `:` when there is no password and nothing when there is no user. Default taken: credentials come from `ITransferContext.Credentials`, so a URL's typed percent-escapes are re-encoded from their decoded form.
- Tests: `HttpConnectionPersistenceTests` (3 rows), `HttpRequestHeadFormatterTests.FtpThroughProxy.cs` (7 cases). Fast tests all green; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 0 failing members.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: e6f6390a 4272a6b9.
- 2026-10-18: Doing -> Done. Upstream cases 208, 299 and 1077 pass: ftp URLs through an HTTP proxy keep user:password, and an HTTP/1.0 proxy's Proxy-Connection: keep-alive keeps the connection and its HTTP/1.0 downgrade
