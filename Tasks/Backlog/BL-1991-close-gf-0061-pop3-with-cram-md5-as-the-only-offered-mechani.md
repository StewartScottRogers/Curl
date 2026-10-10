---
id: BL-1991
title: Close GF-0061: POP3 with CRAM-MD5 as the only offered mechanism sends no AUTH at all
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1991 — Close GF-0061: POP3 with CRAM-MD5 as the only offered mechanism sends no AUTH at all

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0061 (POP3 with CRAM-MD5 as the only offered mechanism sends no AUTH at all), so a later gap analysis measures each of `behaviour:test891` as `match`.

## Context

- Finding: GF-0061, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test891`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test891 (pop3 -u user:secret, CAPA offering only CRAM-MD5, its challenge answered with a bare LF) expected 'upstream test891 passes', actual '<verify><protocol> differs at byte 6 (line 2): expected "AUTH CRAM-MD5\r\n", got the end'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 891

Suggestion, copied from the finding:

In Curl.Protocol.Pop3.UnitLibrary's Pop3Login, send AUTH CRAM-MD5 when CAPA offers it and credentials are given, as curl 8.21.0 does. Read a continuation that ends in a bare LF as a reply line, then fail as curl does.

## Acceptance criteria

- [ ] `behaviour:test891`: Curl answers what curl 8.21.0 answers, `upstream test891 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-10: Created.
