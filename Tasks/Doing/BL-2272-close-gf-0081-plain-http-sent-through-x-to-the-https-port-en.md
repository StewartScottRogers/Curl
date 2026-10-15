---
id: BL-2272
title: Close GF-0081: Plain HTTP sent through -x to the HTTPS port ends in a clean close (exit 52) instead of stunnel's reset (exit 56)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2272 — Close GF-0081: Plain HTTP sent through -x to the HTTPS port ends in a clean close (exit 52) instead of stunnel's reset (exit 56)

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0081 (Plain HTTP sent through -x to the HTTPS port ends in a clean close (exit 52) instead of stunnel's reset (exit 56)), so a later gap analysis measures each of `behaviour:test1244` as `match`.

## Context

- Finding: GF-0081, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test1244`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test1244 expected 'upstream test1244 passes', actual '<verify><errorcode>: expected exit code 56, got 52'. The case's second transfer is '--next http://%HOSTIP:%HTTPPORT/<N>00001 -x %HOSTIP:%HTTPSPORT', so curl speaks plain HTTP to the TLS server; upstream's stunnel drops that connection with a reset and curl reports 56 (Recv failure). Curl.Conformance.UnitLibrary/TlsServerConnection.cs ServeAsync swallows the failed handshake and disposes the in-memory stream, which the client reads as a clean end of stream, so Curl reports 52 (empty reply) for what it was given. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- '<release>/tests/data' $env:TEMP/raw.json 1244

Suggestion, copied from the finding:

In Curl.Conformance.UnitLibrary TlsServerConnection.ServeAsync, end a connection whose handshake fails on a non-TLS first record as a reset rather than a clean close: let InMemoryDuplexStream fault the client's next read with an IOException carrying SocketException(ConnectionReset), as a socket closed with unread data does. Then confirm Curl.Protocol.Http.UnitLibrary maps a reset on the first response read to exit 56 'Recv failure: Connection was reset', with a focused test in Curl.Protocol.Http.UnitTests, and pin the harness change in Curl.Conformance.UnitTests.

## Acceptance criteria

- [ ] `behaviour:test1244`: Curl answers what curl 8.22.0 answers, `upstream test1244 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
