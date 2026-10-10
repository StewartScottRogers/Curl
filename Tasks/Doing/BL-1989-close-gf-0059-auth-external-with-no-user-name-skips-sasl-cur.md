---
id: BL-1989
title: Close GF-0059: ;AUTH=EXTERNAL with no user name skips SASL; curl sends AUTH EXTERNAL with an empty (=) response
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1989 — Close GF-0059: ;AUTH=EXTERNAL with no user name skips SASL; curl sends AUTH EXTERNAL with an empty (=) response

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0059 (;AUTH=EXTERNAL with no user name skips SASL; curl sends AUTH EXTERNAL with an empty (=) response), so a later gap analysis measures each of `behaviour:test838`, `behaviour:test840`, `behaviour:test884`, `behaviour:test886`, `behaviour:test943`, `behaviour:test945` as `match`.

## Context

- Finding: GF-0059, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test838`, `behaviour:test840`, `behaviour:test884`, `behaviour:test886`, `behaviour:test943`, `behaviour:test945`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test838 ('imap://;AUTH=EXTERNAL@host/...'): '<verify><protocol> differs at byte 17 (line 2): expected "A002 AUTHENTICATE EXTERNAL\r\n", got the end'; 840 (SASL-IR) expected 'A002 AUTHENTICATE EXTERNAL ='. test884/886 (pop3): expected 'AUTH EXTERNAL' / 'AUTH EXTERNAL =', got 'RETR 884'. test943/945 (smtp 'external authentication without credentials'): expected 'AUTH EXTERNAL' / 'AUTH EXTERNAL =', got the end. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 838,840,884,943

Suggestion, copied from the finding:

In Curl.Authentication.UnitLibrary's SaslAuthenticator/SaslMechanismRanking and the IMAP, POP3 and SMTP login decisions, let a ;AUTH=EXTERNAL login option start SASL without a user name or password. EXTERNAL's response is the (empty) user name, sent as '=' when empty, inline under SASL-IR or after the '+' / '334' continuation, as curl 8.21.0 does.

## Acceptance criteria

- [ ] `behaviour:test838`: Curl answers what curl 8.21.0 answers, `upstream test838 passes`, so the item measures `match`.
- [ ] `behaviour:test840`: Curl answers what curl 8.21.0 answers, `upstream test840 passes`, so the item measures `match`.
- [ ] `behaviour:test884`: Curl answers what curl 8.21.0 answers, `upstream test884 passes`, so the item measures `match`.
- [ ] `behaviour:test886`: Curl answers what curl 8.21.0 answers, `upstream test886 passes`, so the item measures `match`.
- [ ] `behaviour:test943`: Curl answers what curl 8.21.0 answers, `upstream test943 passes`, so the item measures `match`.
- [ ] `behaviour:test945`: Curl answers what curl 8.21.0 answers, `upstream test945 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
