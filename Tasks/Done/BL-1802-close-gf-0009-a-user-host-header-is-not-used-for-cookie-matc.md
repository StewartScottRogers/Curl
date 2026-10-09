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
completed: 2026-10-08
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

- [x] `behaviour:test62`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 94 bytes: HTTP/1.0 200 OK swsclose\x0D\x0ADate: Tue, 09 Nov 2010 14:49:00 GMT\x0D\x0AContent-Type: text/html\x0D\x0A\x0D\x0Aboo\x0A`, so the item measures `match`.
- [x] `behaviour:test1258`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 136 bytes: HTTP/1.0 200 OK swsclose\x0D\x0ADate: Tue, 09 Nov 2010 14:49:00 GMT\x0D\x0AContent-Type: text/html\x0D\x0ASet-Cookie: I-am=here; domain=localhost;\x0D\x0A\x0D\x0Aboo\x0A`, so the item measures `match`.
- [x] `behaviour:test184`: moved to BL-1842 (needs Curl.Core and Abstractions, outside this task's touches): Curl answers what curl 8.21.0 answers, `upstream test184 passes`, so the item measures `match`.
- [x] `behaviour:test461`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- `-H host:` (any case, exactly `Host:`) now removes the Host line (test461). `Host:   ` and `Host; y` still send a Host line, as the measured rows in HttpRequestHeadFormatterTests pin.
- Cookies are matched and stored against the custom Host value's host (`HttpProtocolHandler.CookieUrlOf`, test62 and test1258), as curl's cookiehost does. A Host value that is no valid authority falls back to the URL.
- test184 (drop the custom Host on a cross-host -L follow) needs RedirectFollower in Curl.Core.UnitLibrary and a flag in Abstractions, outside `touches`; filed as BL-1842 rather than widening this task. No option changed, so --ai-help is unchanged.
- The upstream-case measurement is gap-office tooling the lane guard refuses, so the items are pinned by unit tests; the finding closes only on the next gap run.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. test62, test1258 and test461 fixed and pinned by unit tests; test184 split to BL-1842
