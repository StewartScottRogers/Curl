---
id: BL-1979
title: Close GF-0049: TFTP requests drop every leading slash of the URL path; curl drops only the first, so tftp://host//N asks for /N
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1979 — Close GF-0049: TFTP requests drop every leading slash of the URL path; curl drops only the first, so tftp://host//N asks for /N

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0049 (TFTP requests drop every leading slash of the URL path; curl drops only the first, so tftp://host//N asks for /N), so a later gap analysis measures each of `behaviour:test1007`, `behaviour:test1009`, `behaviour:test1049`, `behaviour:test1093`, `behaviour:test1094`, `behaviour:test1099`, `behaviour:test1238`, `behaviour:test1242`, `behaviour:test1243`, `behaviour:test271`, `behaviour:test283`, `behaviour:test284`, `behaviour:test285`, `behaviour:test286`, `behaviour:test332` as `match`.

## Context

- Finding: GF-0049, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test1007`, `behaviour:test1009`, `behaviour:test1049`, `behaviour:test1093`, `behaviour:test1094`, `behaviour:test1099`, `behaviour:test1238`, `behaviour:test1242`, `behaviour:test1243`, `behaviour:test271`, `behaviour:test283`, `behaviour:test284`, `behaviour:test285`, `behaviour:test286`, `behaviour:test332`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test271 (tftp://%HOSTIP:%TFTPPORT//271): '<verify><protocol> differs at byte 59 (line 5): expected "filename = /271\n", got "filename = 271\n"'. test1007: expected 'filename = /invalid-file', got 'filename = invalid-file'. test1243/285/286 (WRQ): expected 'filename = /test1243.txt', got 'filename = test1243.txt'. Cause: Curl.Protocol.Tftp.UnitLibrary/TftpRequestFile.FromUrlPath does absolutePath.TrimStart('/'), stripping every leading slash. curl's tftp.c skips only the one separator slash (RFC 3617). Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 271,1007,1243

Suggestion, copied from the finding:

In Curl.Protocol.Tftp.UnitLibrary's TftpRequestFile.FromUrlPath, remove exactly one leading '/' from the URL path, not all of them. tftp://host//271 then requests '/271' as curl 8.21.0 does. Pin it in Curl.Protocol.Tftp.UnitTests for RRQ and WRQ.

## Acceptance criteria

- [ ] `behaviour:test1007`: Curl answers what curl 8.21.0 answers, `upstream test1007 passes`, so the item measures `match`.
- [ ] `behaviour:test1009`: Curl answers what curl 8.21.0 answers, `upstream test1009 passes`, so the item measures `match`.
- [ ] `behaviour:test1049`: Curl answers what curl 8.21.0 answers, `upstream test1049 passes`, so the item measures `match`.
- [ ] `behaviour:test1093`: Curl answers what curl 8.21.0 answers, `upstream test1093 passes`, so the item measures `match`.
- [ ] `behaviour:test1094`: Curl answers what curl 8.21.0 answers, `upstream test1094 passes`, so the item measures `match`.
- [ ] `behaviour:test1099`: Curl answers what curl 8.21.0 answers, `upstream test1099 passes`, so the item measures `match`.
- [ ] `behaviour:test1238`: Curl answers what curl 8.21.0 answers, `upstream test1238 passes`, so the item measures `match`.
- [ ] `behaviour:test1242`: Curl answers what curl 8.21.0 answers, `upstream test1242 passes`, so the item measures `match`.
- [ ] `behaviour:test1243`: Curl answers what curl 8.21.0 answers, `upstream test1243 passes`, so the item measures `match`.
- [ ] `behaviour:test271`: Curl answers what curl 8.21.0 answers, `upstream test271 passes`, so the item measures `match`.
- [ ] `behaviour:test283`: Curl answers what curl 8.21.0 answers, `upstream test283 passes`, so the item measures `match`.
- [ ] `behaviour:test284`: Curl answers what curl 8.21.0 answers, `upstream test284 passes`, so the item measures `match`.
- [ ] `behaviour:test285`: Curl answers what curl 8.21.0 answers, `upstream test285 passes`, so the item measures `match`.
- [ ] `behaviour:test286`: Curl answers what curl 8.21.0 answers, `upstream test286 passes`, so the item measures `match`.
- [ ] `behaviour:test332`: Curl answers what curl 8.21.0 answers, `upstream test332 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-10: Created.
