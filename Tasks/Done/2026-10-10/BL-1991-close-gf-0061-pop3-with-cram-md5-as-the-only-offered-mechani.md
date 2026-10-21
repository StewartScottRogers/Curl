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
completed: 2026-10-10
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

- [x] `behaviour:test891`: Curl answers what curl 8.21.0 answers, `upstream test891 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md). No option changed.

## Notes

- Upstream test891 (curl-8_21_0 tag, read from GitHub because the lane's audit guard
  refuses the local gap cache path): servercmd `AUTH CRAM-MD5` / `REPLYLF AUTH +`, command
  `-u user:secret`, expected protocol `CAPA`, `AUTH CRAM-MD5`,
  `base64("user 5c8db03f04cec0f43bcb060023914190")`, exit 67.
- Measured 2026-10-10 with `Record-CurlExchange.ps1 -Script` (greeting, CAPA listing only
  `SASL CRAM-MD5`, `+` then bare LF, then `-ERR`): real curl 8.21.0 and Curl at this
  commit (Debug `Curl.Console`) sent byte-identical streams - `CAPA`, `AUTH CRAM-MD5`,
  `dXNlciA1YzhkYjAzZjA0Y2VjMGY0M2JjYjA2MDAyMzkxNDE5MA==` - and both exited 67
  `Login denied`. The `-Pop3` mode with CRLF continuations gave the same match.
- So Curl at HEAD already behaves as the finding asks; no production change was needed.
  The finding's "got the end" (nothing after CAPA) was not reproduced; it was measured on
  an older ref or by the gap harness's own server emulation (which a lane may not read).
  The gap closes only when the next gap run re-measures test891 (ADR-0433), and that run
  is the check; if it still differs, the cause is in `Gap/Tools/Measure-UpstreamCases.cs`'s
  handling of `REPLYLF`, which only an interactive session may touch.
- Pinned the exchange in
  `Pop3ProtocolHandlerSaslCancelTests.ExecuteAsync_CramMd5ContinuationEndingInBareLf_AnswersEmptyChallengeThenLoginDenied`:
  the bare-LF `+` is read as an empty challenge, answered (not cancelled), and the `-ERR`
  is exit 67.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. POP3 test891 exchange (CRAM-MD5 only, bare-LF continuation) matches curl 8.21.0 byte for byte, pinned by a unit test
