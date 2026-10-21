---
id: BL-2880
title: Keep the sws emulation reading an unfinished request instead of closing (test1548)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs, Curl.Conformance.UnitLibrary/SwsHttpRequestFraming.cs, Curl.Conformance.UnitLibrary/CLAUDE.md, Curl.Conformance.UnitTests/SwsHttpServerConnectorTests.cs, Curl.Conformance.UnitTests/SwsHttpServerConnectorTests.Adversarial.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-19
completed:
---
# BL-2880 — Keep the sws emulation reading an unfinished request instead of closing (test1548)

## Goal

A read on the `sws` emulation while the client's bytes hold an unfinished request waits for the client's next write, as upstream's `sws` blocks reading it, so upstream case 1548 passes and is listed.

## Context

Filed from BL-2870 (ID chosen above the shared board's highest, BL-2874, because this lane's board was stale). Verdict (b), harness wrong. test1548 runs `-m 1 telnet://%HOSTIP:%HTTPPORT` with stdin `GET /we/want/1548 HTTP/1.0\n\n`: curl sends those 28 bytes as they are (line feeds only, no telnet negotiation; measured with `Record-CurlExchange.ps1`, curl 8.22.0 Schannel: exit 28 after 1300 ms with a server that answers nothing). sws never finds `\r\n\r\n`, so it keeps reading and curl's `-m 1` fires. `SwsHttpServerConnection.ReadWithNothingSentAsync` instead returns 0 for any read with no reply waiting unless an `Expect: 100-continue` body is owed, so Curl sees a close and exits 0.

The fix BL-2870 tried (left in its stash): replace `SwsHttpRequestFraming.AwaitsExpectedBody(...)` in `ReadWithNothingSentAsync` with `unservedRequestBytes.Count > 0` (every complete request is served on write, so unserved bytes are always a request sws is still reading), delete the now-unused `AwaitsExpectedBody`, and add `UnfinishedRequest_ReadBeforeItsEnd_WaitsForTheClientsNextWrite`. With it `CURL_UPSTREAM_CASE=1548` passes. But about 15 tests in `SwsHttpServerConnectorTests.cs` (lines near 213, 252, 254, 417, 431, 441, 458, 533, 1138) and `SwsHttpServerConnectorTests.Adversarial.cs` (21, 60) read after a partial request and assert an empty reply; they now hang (first found: `AuthRequired_DoesNotChangeAChunkedRequest`). Rewrite each to check that the read is still pending (as `ExpectContinue_ReadBeforeTheBody_WaitsForTheBodyAndAnswersIt` does) or to cancel it, then run the whole `Curl.Conformance.UnitTests` project with `--blame-hang-timeout 60s` to catch any ratchet case that now waits (each listed case is bounded by its 30-second harness limit; check none drops off).

## Acceptance criteria

- [ ] `$env:CURL_UPSTREAM_CASE='1548'` with `dotnet test Curl.Conformance.UnitTests --filter "TestCategory=Manual"` passes, and 1548 is on `PassingUpstreamCases.txt`.
- [ ] `UnfinishedRequest_ReadBeforeItsEnd_WaitsForTheClientsNextWrite` in `SwsHttpServerConnectorTests` fails without the fix and passes with it.
- [ ] `dotnet test Curl.Conformance.UnitTests --filter "TestCategory!=Integration" --blame-hang-timeout 60s` finishes with no failure and no hang.
- [ ] Build clean, fast tests green, `Curl.Conformance.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10, and its `CLAUDE.md` says a read with an unfinished request waits for the next write.

## Notes

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 9a1e518b 7e9d4057 e4965115 5b83043b.
