---
id: BL-2002
title: Re-close GF-0006: With --digest (or Digest against a proxy), the first POST/PUT sends its body instead of Content-Length: 0
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-2002 — Re-close GF-0006: With --digest (or Digest against a proxy), the first POST/PUT sends its body instead of Content-Length: 0

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0006 (With --digest (or Digest against a proxy), the first POST/PUT sends its body instead of Content-Length: 0), so a later gap analysis measures each of `behaviour:test88`, `behaviour:test175`, `behaviour:test177`, `behaviour:test245`, `behaviour:test246`, `behaviour:test1001`, `behaviour:test1002`, `behaviour:test1284`, `behaviour:test1285`, `behaviour:test2058`, `behaviour:test2059`, `behaviour:test2060`, `behaviour:test2067`, `behaviour:test2068`, `behaviour:test2069` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0006 ([BL-1799]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

- Finding: GF-0006, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test88`, `behaviour:test175`, `behaviour:test177`, `behaviour:test245`, `behaviour:test246`, `behaviour:test1001`, `behaviour:test1002`, `behaviour:test1284`, `behaviour:test1285`, `behaviour:test2058`, `behaviour:test2059`, `behaviour:test2060`, `behaviour:test2067`, `behaviour:test2068`, `behaviour:test2069`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test175 actual: <verify><protocol> differs at byte 96 (line 5): expected 'Content-Length: 0', got 'Content-Length: 11'. test177, test245, test246, test1284 and test2067-2069 have the same shape. test88/test1285: expected 'Content-Length: 0', got 'Content-Length: 85'. test1001/1002/2058-2060: expected 'Content-Length: 0', got 'Content-Length: 3'. The reference curl exits 0 on test177. Cause in code: HttpProtocolHandler frames the first request with its full body even when the authenticator has picked Digest and holds no challenge yet. curl sends that probe with an empty body and Content-Length: 0, overriding a user Content-Length (test1284), and sends the body only on the authenticated request. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 175,88,1001

Suggestion, copied from the finding:

In Curl.Protocol.Http.UnitLibrary, when --digest is chosen for the origin or the proxy and no challenge has been answered yet, frame the first POST or PUT (HttpRequestFraming / HttpRequestBodyWriter) with an empty body and Content-Length: 0, replacing a user-supplied Content-Length. Send the real body on the retry that carries Authorization. Leave the body in place when the server answers the probe without a challenge (test175 then sends it on the next request, as upstream's <verify> shows).

## Acceptance criteria

- [ ] `behaviour:test88`: Curl answers what curl 8.21.0 answers, `upstream test88 passes`, so the item measures `match`.
- [ ] `behaviour:test175`: Curl answers what curl 8.21.0 answers, `upstream test175 passes`, so the item measures `match`.
- [ ] `behaviour:test177`: Curl answers what curl 8.21.0 answers, `upstream test177 passes`, so the item measures `match`.
- [ ] `behaviour:test245`: Curl answers what curl 8.21.0 answers, `upstream test245 passes`, so the item measures `match`.
- [ ] `behaviour:test246`: Curl answers what curl 8.21.0 answers, `upstream test246 passes`, so the item measures `match`.
- [ ] `behaviour:test1001`: Curl answers what curl 8.21.0 answers, `upstream test1001 passes`, so the item measures `match`.
- [ ] `behaviour:test1002`: Curl answers what curl 8.21.0 answers, `upstream test1002 passes`, so the item measures `match`.
- [ ] `behaviour:test1284`: Curl answers what curl 8.21.0 answers, `upstream test1284 passes`, so the item measures `match`.
- [ ] `behaviour:test1285`: Curl answers what curl 8.21.0 answers, `upstream test1285 passes`, so the item measures `match`.
- [ ] `behaviour:test2058`: Curl answers what curl 8.21.0 answers, `upstream test2058 passes`, so the item measures `match`.
- [ ] `behaviour:test2059`: Curl answers what curl 8.21.0 answers, `upstream test2059 passes`, so the item measures `match`.
- [ ] `behaviour:test2060`: Curl answers what curl 8.21.0 answers, `upstream test2060 passes`, so the item measures `match`.
- [ ] `behaviour:test2067`: Curl answers what curl 8.21.0 answers, `upstream test2067 passes`, so the item measures `match`.
- [ ] `behaviour:test2068`: Curl answers what curl 8.21.0 answers, `upstream test2068 passes`, so the item measures `match`.
- [ ] `behaviour:test2069`: Curl answers what curl 8.21.0 answers, `upstream test2069 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
