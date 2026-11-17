---
id: BL-5324
title: Redact user-info passwords in Location and Content-Location headers in the diagnostic log
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpExchangeLog.cs, Curl.Original.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-5324 — Redact user-info passwords in Location and Content-Location headers in the diagnostic log

## Goal

`--log-level verbose --log-file` never writes a password carried in the user-info of a `Location` or `Content-Location` response header.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding EXT-068 (Low, Confirmed, 2 runs; credential in a log the docs promise never holds one). Area: diagnostic log; Windows. This is an extension feature (the diagnostic log), so it is judged against the port's own docs and ADRs, not curl.

**Repro.** A server whose `/rediru` answers `302` with `Location: http://u:LOCSECRET8@127.0.0.1:38000/plain` (PowerShell or a C# file-based app, never Python). `curl -sS -m5 --log-level verbose --log-file dl.log -L http://127.0.0.1:38000/rediru -o NUL`, then `grep LOCSECRET8 dl.log`

**Expected.** ADR-0222 rule 7: "Never logged: passwords, -u and URL user-info passwords". The log's own `[redirect] following 302 to http://127.0.0.1:38000/plain` line already strips it.

**Actual.** `[verbose] [http] header received Location: http://u:LOCSECRET8@127.0.0.1:38000/plain`, exit 0. (Not logged at `info`.)

**Where to start.** `Curl.Original.Protocol.Http.UnitLibrary/HttpExchangeLog.cs`: `SecretHeaders` covers only `Authorization`, `Proxy-Authorization`, `Cookie`, `Set-Cookie`; redact user-info in `Location` and `Content-Location` values (keep the rest of the value, and leave a value with no user-info, or one that is not an absolute URL, unchanged).

## Acceptance criteria

- [x] New tests in `Curl.Original.Protocol.Http.UnitTests` prove a `Location: http://u:LOCSECRET8@host/plain` header logged at `verbose` appears with no `LOCSECRET8` (the user-info replaced with `REDACTED:REDACTED` or removed, the test pinning which), and the same for `Content-Location`.
- [x] A test proves a `Location` with no user-info, a relative `Location: /plain` and a value that is not a URL are logged unchanged, and that the header name match is case-insensitive.
- [x] Tests are platform-neutral and use a stand-in log sink.
- [x] Review acceptance check recorded in the Log as a hand check: `grep -c LOCSECRET8 dl.log` prints 0, and the Location line shows `http://REDACTED:REDACTED@…` or no user-info.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Decision: user-info is replaced by REDACTED:REDACTED (same as HAR). Relative, non-URL and user-info-free values unchanged. Quality run: 0 failing members.
- Review acceptance check: `grep -c LOCSECRET8 dl.log` prints 0, and the Location line shows `http://REDACTED:REDACTED@…` or no user-info.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 53320ebe 02f2afa5 561f41a6 311c564b a0b49a29.
- 2026-11-16: Hand check (unit-level, stand-in log sink, Location "http://u:LOCSECRET8@host/plain" at verbose): logged line is "header received Location: http://REDACTED:REDACTED@host/plain"; LOCSECRET8 count 0.
- 2026-11-16: Doing -> Done. Location and Content-Location user-info redacted in the diagnostic log
