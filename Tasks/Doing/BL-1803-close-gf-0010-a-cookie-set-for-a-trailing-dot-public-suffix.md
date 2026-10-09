---
id: BL-1803
title: Close GF-0010: A cookie set for a trailing-dot public suffix is sent, and a -b name=value cookie follows a redirect to another host
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1803 — Close GF-0010: A cookie set for a trailing-dot public suffix is sent, and a -b name=value cookie follows a redirect to another host

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0010 (A cookie set for a trailing-dot public suffix is sent, and a -b name=value cookie follows a redirect to another host), so a later gap analysis measures each of `behaviour:test1629`, `behaviour:test2015` as `match`.

## Context

- Finding: GF-0010, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test1629`, `behaviour:test2015`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Both items expect 'upstream test<N> passes'. test1629 actual: <verify><protocol> differs at byte 156 (line 10): expected an empty line, got 'Cookie: something=1'. The reply set 'Domain=co.uk.', which the PSL must refuse, and the reference curl exits 0 and sends no cookie. test2015 (-b 'test=yes' -x ... --location to a new host): expected an empty line, got 'Cookie: test=yes'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1629,2015

Suggestion, copied from the finding:

In Curl.Cookies.UnitLibrary: strip a trailing dot from both the Domain attribute and the request host before the PublicSuffixList check, so 'co.uk.' is refused as a public suffix. Bind a -b name=value cookie to the first URL's host, so it is not sent after a redirect to another host, as upstream test2015 expects.

## Acceptance criteria

- [x] `behaviour:test1629`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 92 bytes: HTTP/1.1 200 OK\x0D\x0AContent-Length: 6\x0D\x0ASet-Cookie: something=1; Domain=co.uk.; Path=/\x0D\x0A\x0D\x0A-foo-\x0A`, so the item measures `match`.
- [x] `behaviour:test2015`: moved to BL-1846 (see Notes).
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md). No option changed.

## Notes

- test1629: `PublicSuffixList.IsCookieDomainAcceptable` saw host `www.example.co.uk.` and domain `co.uk.`; with the trailing dot, dropping labels ended at the empty string, so every domain looked longer than the public suffix and was accepted. It now leaves one trailing dot off both before the check, so `co.uk.` is refused as curl refuses it; `Domain=example.co.uk.` from the same host is still kept. Pinned by two new DataRows in `CookieStoreTests.PublicSuffix.cs`.
- test2015 split off to BL-1846 (decided by Claude under the delegation; a task split, not a behaviour decision, so no ADR): the `-b name=value` string has to be dropped on a cross-host redirect, which only the redirect follower knows about. `CookieStore` cannot tell a redirect hop from a second command-line URL, which must still get the string, so the fix belongs in `Curl.Core.UnitLibrary` / `Curl.Protocol.Http.UnitLibrary` / `Curl.Console`, outside this task's `touches`. The gap's test2015 item closes when BL-1846 lands and a gap run re-measures it.
- The upstream test files could not be read from a lane (the audit guard refuses any path with the gap office's folder in it), so the host in test1629 was inferred from the evidence: `co.uk.` can only tail-match a host ending in `co.uk.`.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
