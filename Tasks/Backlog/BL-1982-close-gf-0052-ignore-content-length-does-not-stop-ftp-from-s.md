---
id: BL-1982
title: Close GF-0052: --ignore-content-length does not stop FTP from sending SIZE before RETR
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1982 — Close GF-0052: --ignore-content-length does not stop FTP from sending SIZE before RETR

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0052 (--ignore-content-length does not stop FTP from sending SIZE before RETR), so a later gap analysis measures each of `behaviour:test1137`, `behaviour:test416` as `match`.

## Context

- Finding: GF-0052, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test1137`, `behaviour:test416`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test1137: '<verify><protocol> differs at byte 63 (line 7): expected "RETR 1137\r\n", got "SIZE 1137\r\n"'. test416 (EPSV, Range) the same at byte 57. Curl.Protocol.Ftp.UnitLibrary has no reference to the option. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1137,416

Suggestion, copied from the finding:

Carry --ignore-content-length to the FTP handler (Curl.Console's transfer-context mapping) and, in Curl.Protocol.Ftp.UnitLibrary's FtpSession download path, skip SIZE and go straight to RETR when it is set, as curl 8.21.0 does (data->set.ignorecl).

## Acceptance criteria

- [ ] `behaviour:test1137`: Curl answers what curl 8.21.0 answers, `upstream test1137 passes`, so the item measures `match`.
- [ ] `behaviour:test416`: Curl answers what curl 8.21.0 answers, `upstream test416 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-10: Created.
