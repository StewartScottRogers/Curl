---
id: BL-1994
title: Close GF-0064: --crlf does not convert an SMTP upload's LF line ends to CR LF
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1994 — Close GF-0064: --crlf does not convert an SMTP upload's LF line ends to CR LF

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0064 (--crlf does not convert an SMTP upload's LF line ends to CR LF), so a later gap analysis measures each of `behaviour:test941` as `match`.

## Context

- Finding: GF-0064, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test941`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test941 (smtp -T upload --crlf) expected 'upstream test941 passes', actual '<verify><upload> differs at byte 15 (line 1): expected "From: different\r\n", got "From: different\n"'. Curl.Protocol.Smtp.UnitLibrary and the mail options have no --crlf setting. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 941

Suggestion, copied from the finding:

Carry --crlf into MailRequestOptions (Curl.Console's MailRequestOptionsMapping, Curl.Protocol.Abstractions.UnitLibrary). In Curl.Protocol.Smtp.UnitLibrary, convert each bare LF of the upload to CR LF before dot-stuffing (SmtpDotStuffer), as curl 8.21.0 does.

## Acceptance criteria

- [ ] `behaviour:test941`: Curl answers what curl 8.21.0 answers, `upstream test941 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-10: Created.
