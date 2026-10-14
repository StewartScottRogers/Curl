---
id: BL-2164
title: Close GF-0072: --next transfers reuse a connection where curl opens a new one (another host name at the same --resolve address) and open one where curl reuses (only --connect-to differs)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-13
completed:
---
# BL-2164 — Close GF-0072: --next transfers reuse a connection where curl opens a new one (another host name at the same --resolve address) and open one where curl reuses (only --connect-to differs)

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0072 (--next transfers reuse a connection where curl opens a new one (another host name at the same --resolve address) and open one where curl reuses (only --connect-to differs)), so a later gap analysis measures each of `behaviour:test2051`, `behaviour:test3225` as `match`.

## Context

- Finding: GF-0072, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test2051`, `behaviour:test3225`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test2051 (three --next GETs of one URL, the first with --connect-to ::%HOSTIP:%HTTPPORT, each -w '%{num_connects}<LF>'): expected 'reference curl exits 0; stdout 111 bytes: ... OK 1 OK 0 OK 0', actual '<verify><stdout> differs at byte 104 (line 9): expected 0<LF>, got 1<LF>' - the second group opens a new connection where the reference curl reuses the first. behaviour:test3225 (host-a.example.test, then after --next host-b.example.test, both mapped by --resolve to %HOSTIP, with -o): expected 'upstream test3225 passes', actual '<verify><protocol> differs at byte 201 (line 12): expected [DISCONNECT]<LF>, got the end' - the server sees one connection close where upstream opens and closes two. OptionGroupConnectionSettings (Curl.Console/OptionGroupConnectionSettings.cs) carries the --connect-to entries verbatim, so groups that differ only there never share. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- $env:LOCALAPPDATA/Curl/gap/upstream/8.22.0/tests/data $env:TEMP/raw.json 2051,3225

Suggestion, copied from the finding:

Align the connection match with curl 8.22.0's. In Curl.Console's OptionGroupConnectionSettings, drop the --connect-to entries from the compared settings, since ConnectionPoolKey already holds the host and port dialled (test2051: the mapping to the same host:port leaves the second and third groups on the first connection, num_connects 0). In Curl.Networking.UnitLibrary's ConnectionPoolKey and PoolingConnector, make the key's Host the URL's host name, never the --resolve address, so host-a and host-b at one address get one connection each (test3225, 'connects=1/connects=1/'). Rerun test3225 with --keep-logs first to confirm Curl reuses the connection rather than leaving the second one unclosed.

## Acceptance criteria

- [ ] `behaviour:test2051`: Curl answers what curl 8.22.0 answers, `reference curl exits 0; stdout 111 bytes: HTTP/1.1 200 OK\x0ADate: Tue, 09 Nov 2010 14:49:00 GMT\x0AContent-Length: 3\x0AContent-Type: text/plain\x0A\x0AOK\x0A1\x0AOK\x0A0\x0AOK\x0A0\x0A`, so the item measures `match`.
- [ ] `behaviour:test3225`: Curl answers what curl 8.22.0 answers, `upstream test3225 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
