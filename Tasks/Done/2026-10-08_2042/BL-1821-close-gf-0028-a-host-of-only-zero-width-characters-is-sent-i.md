---
id: BL-1821
title: Close GF-0028: A host of only zero-width characters is sent instead of failing with exit 3
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1821 — Close GF-0028: A host of only zero-width characters is sent instead of failing with exit 3

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0028 (A host of only zero-width characters is sent instead of failing with exit 3), so a later gap analysis measures each of `behaviour:test763` as `match`.

## Context

- Finding: GF-0028, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test763`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test763 (URL is U+200B U+200C) expected 'upstream test763 passes', actual: <verify><errorcode>: expected exit code 3, got 52. Cause in code: Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.ToPunycode keeps the decoded name when IdnMapping.GetAscii throws, so a name that IDNA maps to nothing is accepted. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 763

Suggestion, copied from the finding:

In Curl.Protocol.Abstractions.UnitLibrary's CurlUrlHost, reject a host whose IDNA mapping (UTS #46, ignored code points removed) leaves an empty name: a malformed URL, exit 3. Keep the existing acceptance of a%80b and a%FFb.

## Acceptance criteria

- [x] `behaviour:test763`: Curl answers what curl 8.21.0 answers, `upstream test763 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- CurlUrlHost.TryNormalizeName now rejects (BadHostname, exit 3) a name made only of dots and characters UTS #46 (transitional) maps to nothing: U+00AD, U+034F, U+180B-U+180F, U+200B-U+200D, U+2060, U+FE00-U+FE0F, U+FEFF. Chosen over checking for an IdnMapping exception because .NET also throws for a%80b and a%FFb, which curl accepts. A zero-width character inside a real name (a U+200B b) is still accepted as before. Supplementary-plane ignorables (U+1BCA0.., U+E0100..) are left out: no upstream case needs them. Measured: curl.exe -sS "http://U+200B U+200C/" now prints "curl: (3) URL rejected: Bad hostname", exit 3. No option changed, so --ai-help is untouched.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. A host of only zero-width characters (U+200B U+200C) is now rejected with exit 3, Bad hostname, as curl 8.21.0 does (test763).
