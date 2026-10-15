---
id: BL-2241
title: Make case 1586 pass: HSTS via an https proxy sends the second http GET once
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2226]
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2241 — Make case 1586 pass: HSTS via an https proxy sends the second http GET once

## Goal

Upstream case 1586 (HSTS via an `https://` proxy, ignored for `http:` URLs) passes through Curl and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

BL-2226 gave the harness an `https-proxy` stand-in (`HttpsProxyServerConnector`, `%HTTPSPROXYPORT`), and 1586 now runs and fails with: `<verify><proxy> differs at byte 288 (line 13): expected the end, got "GET http://another.example.com/15860002 HTTP/1.1"` - Curl sends the second URL's proxied GET twice through the HTTPS proxy where curl sends it once. Command: `-x https://%HOSTIP:%HTTPSPROXYPORT --proxy-insecure --hsts %LOGDIR/hsts%TESTNUMBER http://this.hsts.example./%TESTNUMBER http://another.example.com/%TESTNUMBER0002`. Reproduce with `CURL_UPSTREAM_CASE=1586` and the `Manual` test (see `Curl.Conformance.UnitTests/CLAUDE.md`), read the kept trace, and find whether the retry is Curl's (connection reuse over a TLS proxy connection, likely in the HTTP or connection-reuse library) or the stand-in's (the relay closing a kept-alive proxy connection). First step: decide the library to fix and add it to `touches`.

## Acceptance criteria

- [x] test1586 passes and is listed in `PassingUpstreamCases.txt`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the fix (ADR-0482).
- [x] `dotnet build -warnaserror` clean; `dotnet test --filter "TestCategory!=Integration"` green.

## Notes

- The retry was the stand-in's, not Curl's. The kept trace showed the second GET going out on the
  reused proxy connection, reaching sws (it is in `<verify><proxy>` twice), then `Connection died`
  before any reply byte. `TlsRelayConnection` reads its server eagerly, and the sws emulation
  returns 0 from a read with no reply waiting, so after the first reply the relay sent
  close_notify. Real sws blocks reading the next request on a kept-alive connection. Curl's
  retry on a fresh connection is correct for a connection that really died.
- Fix: `Curl.Conformance.UnitLibrary/SwsKeptAliveConnection.cs` wraps the proxy-port sws
  connection in `HttpsProxyServerConnector`. While sws still reads requests, a read with no reply
  waits for the client's next write and reads again. After a close, or after a CONNECT hands the
  connection to a tunnelled server, it returns what sws returns. Nothing changes for the mail,
  FTPS, MQTTS or Gophers relays. Added `Curl.Conformance.UnitLibrary` to `touches`: no task in
  Doing on origin/work/dark-factory named it.
- Tests: `SwsKeptAliveConnectionTests` (waits for the next request; ends after swsclose;
  properties) and `HttpsProxyServerConnectorTests.ConnectAsync_SecondProxiedGetOnTheSameConnection_IsAnsweredToo`.
  The quality gate for Curl.Conformance.UnitLibrary measured 100% line, 100% branch, worst CRAP 10.
  The full fast suite is green, with Curl.Conformance.UnitTests at 2936 passed and 0 failed.
- ADR-0482's "fixed library" is the harness library here. Curl itself is unchanged, so no product
  unit test applies.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. test1586 passes: the https-proxy stand-in keeps its sws proxy connection alive between requests, so curl's second proxied GET goes out once
