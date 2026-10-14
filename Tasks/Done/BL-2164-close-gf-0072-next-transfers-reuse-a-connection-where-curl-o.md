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
completed: 2026-10-13
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

- [x] `behaviour:test2051`: Curl answers what curl 8.22.0 answers, `reference curl exits 0; stdout 111 bytes: HTTP/1.1 200 OK\x0ADate: Tue, 09 Nov 2010 14:49:00 GMT\x0AContent-Length: 3\x0AContent-Type: text/plain\x0A\x0AOK\x0A1\x0AOK\x0A0\x0AOK\x0A0\x0A`, so the item measures `match`.
- [x] `behaviour:test3225`: moved to BL-2178 (split; see Notes). Curl's own behaviour already matches: two connections, `connects=1/connects=1/`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md). No option changed.

## Notes

- test2051 fixed: `OptionGroupConnectionSettings` no longer compares the raw `--connect-to` text. Instead `ConnectionPoolKey` carries `ConnectToAuthority`, the `host:port` a group's `--connect-to` mapping dials when it moves the target off its own host and port (never for a forward proxy). `PoolingConnector` takes the group's `ConnectToMappings` (new optional constructor argument, passed on by `Over`), and `CurlComposition.CreatePoolingConnector` hands it over. Groups whose mappings dial the same place now share a connection, as curl 8.22.0's do; groups dialled to different places still do not. The upstream case passes through `CURL_UPSTREAM_CASE=2051` (Curl.Conformance manual test). Focused tests: `PoolingConnectorSharedCacheTests.ConnectAsync_InALaterGroupWithoutConnectTo_ReusesOnlyWhenTheEarlierMappingDialledTheOrigin` (5 rows) and `..._ToAForwardProxyWithAConnectToMappingThatMovesIt_...` (ADR-0482).
- test3225 split to BL-2178. With keep-logs, Curl opens two connections (stdout `connects=1/connects=1/`, trace `Connection #0 to host host-a...` and `#1 to host host-b...` left intact) and the pool closes both at exit. The finding's suggestion (key on the URL host, not the `--resolve` address) is already how `ConnectionPoolKey` works. The missing second `[DISCONNECT]` comes from the conformance harness: `SwsServerRecording` keeps one server-wide monitor flag and clears it at the first close. That is in `Curl.Conformance.UnitLibrary`, outside this task's touches, so BL-2178 covers it.
- The gap tool (`Gap/Tools`) and `%LOCALAPPDATA%\Curl\gap` are refused to lanes by the audit guard. Measured through the repo's own `Curl.Conformance.UnitTests/UpstreamTestData` copy instead.
- Coverage was not measured with Measure-CodeQuality (time budget). The new branch in `ConnectionPoolKey.ConnectToAuthorityOf` is reached by the new tests' rows: no mappings, forward proxy, unmapped, mapped to self, host moved, port moved.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. --next groups whose --connect-to mappings dial the same host and port share a connection (test2051 passes); test3225's harness fix split to BL-2178
