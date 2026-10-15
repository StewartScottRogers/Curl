---
id: BL-2241
title: Make case 1586 pass: HSTS via an https proxy sends the second http GET once
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2226]
touches: [Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed:
---
# BL-2241 — Make case 1586 pass: HSTS via an https proxy sends the second http GET once

## Goal

Upstream case 1586 (HSTS via an `https://` proxy, ignored for `http:` URLs) passes through Curl and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

BL-2226 gave the harness an `https-proxy` stand-in (`HttpsProxyServerConnector`, `%HTTPSPROXYPORT`), and 1586 now runs and fails with: `<verify><proxy> differs at byte 288 (line 13): expected the end, got "GET http://another.example.com/15860002 HTTP/1.1"` - Curl sends the second URL's proxied GET twice through the HTTPS proxy where curl sends it once. Command: `-x https://%HOSTIP:%HTTPSPROXYPORT --proxy-insecure --hsts %LOGDIR/hsts%TESTNUMBER http://this.hsts.example./%TESTNUMBER http://another.example.com/%TESTNUMBER0002`. Reproduce with `CURL_UPSTREAM_CASE=1586` and the `Manual` test (see `Curl.Conformance.UnitTests/CLAUDE.md`), read the kept trace, and find whether the retry is Curl's (connection reuse over a TLS proxy connection, likely in the HTTP or connection-reuse library) or the stand-in's (the relay closing a kept-alive proxy connection). First step: decide the library to fix and add it to `touches`.

## Acceptance criteria

- [ ] test1586 passes and is listed in `PassingUpstreamCases.txt`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the fix (ADR-0482).
- [ ] `dotnet build -warnaserror` clean; `dotnet test --filter "TestCategory!=Integration"` green.

## Notes

## Log

- 2026-10-14: Created.
