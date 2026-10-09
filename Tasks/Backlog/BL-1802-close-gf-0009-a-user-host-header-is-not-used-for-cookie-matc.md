---
id: BL-1802
title: Close GF-0009: A user Host: header is not used for cookie matching, is kept on a redirect to another host, and a lower-case 'host:' does not remove it
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1802 — Close GF-0009: A user Host: header is not used for cookie matching, is kept on a redirect to another host, and a lower-case 'host:' does not remove it

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0009 (A user Host: header is not used for cookie matching, is kept on a redirect to another host, and a lower-case 'host:' does not remove it), so a later gap analysis measures each of `behaviour:test62`, `behaviour:test1258`, `behaviour:test184`, `behaviour:test461` as `match`.

## Context

- Finding: GF-0009, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test62`, `behaviour:test1258`, `behaviour:test184`, `behaviour:test461`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test62 actual: <verify><protocol> differs at byte 88 (line 5): expected 'Cookie: test2=yes; test=yes', got an empty line. test1258 (-H 'Host: localhost'): expected 'Cookie: I-am=here', got an empty line. test184 (-L -H 'Host: another.visitor...' through a proxy, Location to yet.another.host): expected 'Host: yet.another.host', got 'Host: another.visitor.stay.a.while.stay.foreeeeeever'. test461 (-H host:): expected 'User-Agent: curl/8.21.0', got 'Host:'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 62,1258,184,461

Suggestion, copied from the finding:

In Curl.Protocol.Http.UnitLibrary (HttpCustomHeader, HttpRequestHeadFormatter) and the cookie lookup in Curl.Cookies.UnitLibrary: match header names case-insensitively, so '-H host:' removes Host. On the first request, use a custom Host header's name as the cookie host, as curl does. Drop the custom Host when -L follows to a different host (curl's 'this_is_a_follow' with a host change).

## Acceptance criteria

- [ ] `behaviour:test62`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 94 bytes: HTTP/1.0 200 OK swsclose\x0D\x0ADate: Tue, 09 Nov 2010 14:49:00 GMT\x0D\x0AContent-Type: text/html\x0D\x0A\x0D\x0Aboo\x0A`, so the item measures `match`.
- [ ] `behaviour:test1258`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 136 bytes: HTTP/1.0 200 OK swsclose\x0D\x0ADate: Tue, 09 Nov 2010 14:49:00 GMT\x0D\x0AContent-Type: text/html\x0D\x0ASet-Cookie: I-am=here; domain=localhost;\x0D\x0A\x0D\x0Aboo\x0A`, so the item measures `match`.
- [ ] `behaviour:test184`: Curl answers what curl 8.21.0 answers, `upstream test184 passes`, so the item measures `match`.
- [ ] `behaviour:test461`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-08: Created.
