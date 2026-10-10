---
id: BL-1993
title: Close GF-0063: --mail-from '<addr> RET=HDRS' gains a trailing '>' after the parameters; curl sends an address that starts with '<' as given
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1993 — Close GF-0063: --mail-from '<addr> RET=HDRS' gains a trailing '>' after the parameters; curl sends an address that starts with '<' as given

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0063 (--mail-from '<addr> RET=HDRS' gains a trailing '>' after the parameters; curl sends an address that starts with '<' as given), so a later gap analysis measures each of `behaviour:test3215` as `match`.

## Context

- Finding: GF-0063, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test3215`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test3215 (--mail-from "<sender@example.com> RET=HDRS") expected 'upstream test3215 passes', actual '<verify><protocol> differs at byte 50 (line 2): expected "MAIL FROM:<sender@example.com> RET=HDRS\r\n", got "MAIL FROM:<sender@example.com> RET=HDRS>\r\n"'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 3215

Suggestion, copied from the finding:

In Curl.Protocol.Smtp.UnitLibrary's SmtpMailTransaction, send a --mail-from (and --mail-rcpt) value that already starts with '<' as given, adding no closing '>', as curl 8.21.0's smtp.c does, so DSN parameters (RET=, NOTIFY=) survive.

## Acceptance criteria

- [ ] `behaviour:test3215`: Curl answers what curl 8.21.0 answers, `upstream test3215 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
