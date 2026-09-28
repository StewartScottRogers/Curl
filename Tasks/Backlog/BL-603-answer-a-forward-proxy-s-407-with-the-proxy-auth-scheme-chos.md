---
id: BL-603
title: Answer a forward proxy's 407 with the proxy auth scheme chosen
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-601]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-603 — Answer a forward proxy's 407 with the proxy auth scheme chosen

## Goal

A plain `http://` request through `-x` that the proxy answers `407` is retried with `Proxy-Authorization` for the scheme the proxy auth set and libcurl's ranking pick, alongside any server `Authorization`, as curl 8.21.0 does.

## Context

- Conformance audit 2026-09-28, row 14 (Major). Options: BL-601; NTLM and Negotiate: BL-604.
- Forward-proxy requests are written by `Curl.Protocol.Http.UnitLibrary` (FR-090); the 401 retry logic (ADR-0034) is the model for 407. The authenticator is injected through `IHttpAuthenticator`.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Connections 2` as the proxy: `-x http://127.0.0.1:<P> -U u:p http://example.invalid/` with `--proxy-basic`, `--proxy-digest`, `--proxy-anyauth`, and both `-U` and `-u` with a `407` then a `401`; request bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Http.UnitTests` pin the requests and outcome for each case.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
