---
id: BL-2082
title: Re-close GF-0061: POP3 with CRAM-MD5 as the only offered mechanism sends no AUTH at all
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
lane: no
requirement: none
created: 2026-10-11
completed:
---
# BL-2082 — Re-close GF-0061: POP3 with CRAM-MD5 as the only offered mechanism sends no AUTH at all

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0061 (POP3 with CRAM-MD5 as the only offered mechanism sends no AUTH at all), so a later gap analysis measures each of `behaviour:test891` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0061 ([BL-1991]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

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

- 2026-10-11, dark factory lane 4: Interactive only. Curl's side already matches curl
  8.21.0: BL-1991 measured test891's exchange with `Record-CurlExchange.ps1` (CAPA listing
  only `SASL CRAM-MD5`, `+` then bare LF) and real curl and Curl sent byte-identical
  streams (`CAPA`, `AUTH CRAM-MD5`, the CRAM-MD5 answer), both exit 67; a unit test pins it.
  Read again this run: `Pop3ControlChannel` ends a line at LF with or without CR, and
  `SaslAuthenticator`/`SaslMechanismRanking` choose CRAM-MD5 with `-u user:secret`. Curl
  sends nothing after `CAPA` only when no mechanism is chosen and `CAPA` lacks `USER`, so
  the gap run's "got the end" most likely comes from how `Gap/Tools/Measure-UpstreamCases.cs`
  serves test891 (its `CAPA` reply or its `REPLYLF` handling), which the audit guard
  refuses a lane to read (the local upstream copy under `%LOCALAPPDATA%\Curl\gap` is
  refused too). No code changed.
- Remove `lane: no` once an interactive session has run the finding's reproduce command
  (`dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- ... 891`), read the bytes the
  harness served and Curl sent, and either fixed the harness (then this task closes on the
  next gap run) or written under these Notes the exact server bytes after which Curl stops,
  so a lane can reproduce them in a `Pop3ProtocolHandler` unit test.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Backlog. Interactive only: Curl already matches curl on test891 (BL-1991); the gap office's measuring harness, which lanes may not read, must be checked (see Notes)
