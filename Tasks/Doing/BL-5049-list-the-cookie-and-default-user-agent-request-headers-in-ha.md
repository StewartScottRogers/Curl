---
id: BL-5049
title: List the Cookie and default User-Agent request headers in --har entries
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Har.UnitLibrary/HarExchange.cs, Curl.Extension.Har.UnitLibrary/HarTransferHook.cs, Curl.Extension.Har.UnitTests/HarTransferHookTests.cs, Documentation/Planning/Decisions/ADR-1146-har-writes-each-option-groups-http-transfers-as-a-har-1-2-archive.md]
requirement: none
created: 2026-11-12
completed:
---
# BL-5049 — List the Cookie and default User-Agent request headers in --har entries

## Goal

A `--har` request entry lists the `Cookie` header sent for `-b` and the default `User-Agent` in `request.headers`.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip), finding EXT-041 (Low, Confirmed).

**Repro.** Run `curl -sS --har h3.har -b "sid=abc" http://127.0.0.1:48000/plain -o NUL` against any HTTP server. Inspect `log.entries[0].request.headers` and `.cookies` in `h3.har`.

**Expected.** ADR-1146 lists `User-Agent` "from curl's options" and leaves the `cookies` arrays empty because "the cookies are already in the `Cookie` and `Set-Cookie` headers". So the entry lists `Cookie: sid=abc` and `User-Agent: curl/8.22.0`.

**Actual.** `headers` is `[]` and `cookies` is `[]`, so `sid=abc` appears nowhere. The default `User-Agent` is listed only when `-A` is given.

**Where to start.** `Curl.Extension.Har.UnitLibrary/HarExchange.cs` and `HarTransferHook.cs`, where the request header list is built.

## Acceptance criteria

- [x] `Curl.Extension.Har.UnitTests/HarTransferHookTests.cs` has a test that a transfer with `-b "sid=abc"` produces an entry whose request headers include `Cookie: sid=abc`, and `cookies` stays `[]` as ADR-1146 states.
- [x] A test in the same file shows the default `User-Agent` (`curl/` plus the version the port reports, `curl/8.22.0` in the review) is listed when `-A` is not given, and the `-A` value is listed when it is.
- [x] Regression: `-H` headers, `Host` and the existing listed headers keep their order and content, and no header is listed twice.
- [x] ADR-1146 (`Documentation/Planning/Decisions/ADR-1146-har-writes-each-option-groups-http-transfers-as-a-har-1-2-archive.md`) states that the entry lists the `Cookie` header and the default `User-Agent`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Unit tests use the hook's scripted transfer context, no server. Do not hard-code `8.22.0` in the test; take the version from the same source the default `User-Agent` header uses.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 97d216bb 8a590c81 c8d40aba 5217e918 a0b49a29.
- 2026-11-12: Default UA is a `curl/8.22.0` constant in HarTransferHook (no shared version source exists; same literal as other libraries). Cookie listed after Referer unless a `-H Cookie` is given. Quality run: 0 failing members.
