---
id: BL-2953
title: Give upstream case 2100 the five-digit HTTPSPORT its HTTPS-RR query length assumes
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitTests/UpstreamConformanceTests.NamedCase.cs, Curl.Conformance.UnitLibrary/HttpsServerConnector.cs, Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs, Curl.Conformance.UnitLibrary/CLAUDE.md, Curl.Conformance.UnitTests/HttpsServerConnectorTests.cs, Curl.Conformance.UnitTests/UpstreamCaseRunnerTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2953 — Give upstream case 2100 the five-digit HTTPSPORT its HTTPS-RR query length assumes

## Goal

`CURL_UPSTREAM_CASE=2100` gets past `<protocol>` line 5: the HTTPS-RR DoH POST's `Content-Length: 47` matches, because `%HTTPSPORT` is a five-digit port as upstream's test servers' ports are.

## Context

Found by BL-2920 (2026-10-21). Curl now sends the DoH HTTPS query first (ADR-0727), and case 2100 fails with `<verify><protocol> differs at byte 116 (line 5): expected "Content-Length: 47", got "Content-Length: 46"`. Upstream's `test2100` hard-codes `Content-Length: 47`, which is the length of the query for `_<port>._https.foo.example.com` only when `%HTTPSPORT` has five digits (upstream's runner picks ports above 9999); the harness gave it four. Find where the harness picks `%HTTPSPORT` (start from `UpstreamConformanceTests.NamedCase.cs` and the sws/`https` server setup in `Curl.Conformance.UnitLibrary`) and make every port it substitutes five digits, or pin the port for cases whose expected bytes depend on its length. Narrow or widen `touches` to the file that picks the port before editing. Remaining 2100 work after this is BL-2837's (the `CURL_DBG_HE_AAAA_AWAIT_MS` stand-in).

## Acceptance criteria

- [x] `CURL_UPSTREAM_CASE=2100 dotnet test Curl.Conformance.UnitTests --filter "FullyQualifiedName~NamedUpstreamCase"` no longer reports a difference at `Content-Length: 47`.
- [x] A unit test pins that the harness's substituted `%HTTPSPORT` has five digits.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- %HTTPSPORT was 8989 (HttpsServerConnector.HttpsPort and UpstreamCaseRunner.HttpsPort); both are now 18989. Widened touches to the files that name it. Case 2100 now passes in full on Windows; it is not added to PassingUpstreamCases.txt because only Windows was measured (add it once Linux and macOS are checked).

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 7ea7aab7 521a8aa5 c5f8cef6.
- 2026-10-21: Doing -> Done. HTTPSPORT is now 18989; case 2100 passes
