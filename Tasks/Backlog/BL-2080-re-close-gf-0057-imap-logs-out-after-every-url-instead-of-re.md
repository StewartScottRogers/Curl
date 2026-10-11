---
id: BL-2080
title: Re-close GF-0057: IMAP logs out after every URL instead of reusing the connection, and every connection's tags start with A where curl uses one letter per connection
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-11
completed:
lane: no
---
# BL-2080 — Re-close GF-0057: IMAP logs out after every URL instead of reusing the connection, and every connection's tags start with A where curl uses one letter per connection

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0057 (IMAP logs out after every URL instead of reusing the connection, and every connection's tags start with A where curl uses one letter per connection), so a later gap analysis measures each of `behaviour:test1982`, `behaviour:test804`, `behaviour:test815`, `behaviour:test816`, `behaviour:test836`, `behaviour:test779` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0057 ([BL-1987]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

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
- [ ] `behaviour:test836`: Curl answers what curl 8.21.0 answers, as the Evidence above shows, so the item measures `match`.
- [ ] `behaviour:test779`: Curl answers what curl 8.21.0 answers, `upstream test779 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- 2026-10-11, lane 3 (interactive only): the gap could not be reproduced from a lane.
  BL-1987's fix (0ea0145bb, ADR-0469) is on `master` and was there before gap run
  2026-10-11. Measured with `Record-CurlExchange.ps1 -Imap` and this tree's curl
  (`dotnet run --no-build --project Curl.Console`): test804's two URLs
  (`;MAILINDEX=123/;SECTION=1`, `;MAILINDEX=456/;SECTION=2.3`, `-u user:secret`) give
  A001 CAPABILITY, A002 AUTHENTICATE PLAIN, A003 SELECT, A004 FETCH 123 BODY[1],
  A005 FETCH 456 BODY[2.3], A006 LOGOUT on one connection, exit 0 - test804's expected
  sequence. So the measured A005 LOGOUT depends on something in the upstream case, its
  server or how the harness invokes Curl, as for GF-0051/BL-2079 (FTP, same cause class).
  The two-user case (test836, B001) could not be checked here: the recorder serves one
  connection only. The reproduction (`Gap/Tools/Measure-UpstreamCases.cs`) and the
  upstream data under `%LOCALAPPDATA%\Curl\gap\` are refused to lanes by the audit guard.
- Remove `lane: no` once an interactive session has run
  `Measure-UpstreamCases.cs ... 804,815,836,779` on the current tree, captured test804's
  and test836's protocol transcripts, and written under Notes which reply, step or harness
  difference makes Curl send LOGOUT after the first URL and tag a second connection A001,
  so a lane can fix it without the Gap paths. BL-2079 has the same open question for FTP;
  one harness investigation likely answers both.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Backlog. requeue as interactive only; this tree keeps the IMAP connection for a test804-shaped run, so GF-0057 differs only under the gap harness, whose paths lanes cannot read
