---
id: BL-1992
title: Close GF-0062: SMTP AUTH PLAIN over two URLs with -: sends an empty authorization identity; upstream expects user, user, password
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1992 — Close GF-0062: SMTP AUTH PLAIN over two URLs with -: sends an empty authorization identity; upstream expects user, user, password

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0062 (SMTP AUTH PLAIN over two URLs with -: sends an empty authorization identity; upstream expects user, user, password), so a later gap analysis measures each of `behaviour:test938` as `match`.

## Context

- Finding: GF-0062, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test938`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test938 (two smtp URLs joined by -:, -u user.one:secret then user.two:secret) expected 'upstream test938 passes', actual '<verify><protocol> differs at byte 25 (line 3): expected "dXNlci5vbmUAdXNlci5vbmUAc2VjcmV0\r\n", got "AHVzZXIub25lAHNlY3JldA==\r\n"': curl sends 'user.one NUL user.one NUL secret' and Curl sends 'NUL user.one NUL secret'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 938

Suggestion, copied from the finding:

Measure test938's command line against the reference with Record-CurlExchange.ps1 -Smtp to confirm when curl 8.21.0 fills PLAIN's authorization identity with the user name. Then make Curl.Authentication.UnitLibrary's PLAIN message (and Curl.Protocol.Smtp.UnitLibrary's SmtpSaslAuthentication) build it the same way, keeping the empty identity where test833-style cases expect it.

## Acceptance criteria

- [ ] `behaviour:test938`: Curl answers what curl 8.21.0 answers, `upstream test938 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-10: Created.
