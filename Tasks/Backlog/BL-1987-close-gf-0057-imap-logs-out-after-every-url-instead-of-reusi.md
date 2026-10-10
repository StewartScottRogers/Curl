---
id: BL-1987
title: Close GF-0057: IMAP logs out after every URL instead of reusing the connection, and every connection's tags start with A where curl uses one letter per connection
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1987 — Close GF-0057: IMAP logs out after every URL instead of reusing the connection, and every connection's tags start with A where curl uses one letter per connection

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0057 (IMAP logs out after every URL instead of reusing the connection, and every connection's tags start with A where curl uses one letter per connection), so a later gap analysis measures each of `behaviour:test1982`, `behaviour:test804`, `behaviour:test815`, `behaviour:test816`, `behaviour:test836`, `behaviour:test779` as `match`.

## Context

- Finding: GF-0057, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test1982`, `behaviour:test804`, `behaviour:test815`, `behaviour:test816`, `behaviour:test836`, `behaviour:test779`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test804 (two URLs, same mailbox): '<verify><protocol> differs at byte 87 (line 5): expected "A005 FETCH 456 BODY[2.3]\r\n", got "A005 LOGOUT\r\n"'. 815/816 (-X STORE then -X CLOSE/EXPUNGE): expected 'A005 CLOSE' or 'A005 EXPUNGE', got 'A005 LOGOUT'. 1982: expected 'A005 UID FETCH 2 BODY[]', got 'A005 LOGOUT'. test836 (second user, so a second connection) and test779 (http redirect to imap): expected 'B001 CAPABILITY', got 'A001 CAPABILITY'. Cause: Curl.Protocol.Imap.UnitLibrary's ImapControlChannel builds every tag as $"A{commandId:D3}", and the session always sends LOGOUT and closes. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 804,815,836,779

Suggestion, copied from the finding:

In Curl.Protocol.Imap.UnitLibrary, keep an IMAP connection open after a transfer and hand it to the run's connection cache, keyed by host, port, user and auth mechanism. A next URL on it continues the tag count (A005 ...) without a second login, and LOGOUT is sent when the cache closes it at exit. Make the tag letter 'A' plus the connection's number mod 26 (curl's imap.c), taking the number from the run's IConnectionNumbers, so a second connection's tags start B001. Pin both in Curl.Protocol.Imap.UnitTests.

## Acceptance criteria

- [ ] `behaviour:test1982`: Curl answers what curl 8.21.0 answers, `upstream test1982 passes`, so the item measures `match`.
- [ ] `behaviour:test804`: Curl answers what curl 8.21.0 answers, `upstream test804 passes`, so the item measures `match`.
- [ ] `behaviour:test815`: Curl answers what curl 8.21.0 answers, `upstream test815 passes`, so the item measures `match`.
- [ ] `behaviour:test816`: Curl answers what curl 8.21.0 answers, `upstream test816 passes`, so the item measures `match`.
- [ ] `behaviour:test836`: Curl answers what curl 8.21.0 answers, `upstream test836 passes`, so the item measures `match`.
- [ ] `behaviour:test779`: Curl answers what curl 8.21.0 answers, `upstream test779 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-10: Created.
