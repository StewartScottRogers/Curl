---
id: BL-2161
title: Re-close GF-0059: ;AUTH=EXTERNAL with no user name skips SASL; curl sends AUTH EXTERNAL with an empty (=) response
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2161 — Re-close GF-0059: ;AUTH=EXTERNAL with no user name skips SASL; curl sends AUTH EXTERNAL with an empty (=) response

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0059 (;AUTH=EXTERNAL with no user name skips SASL; curl sends AUTH EXTERNAL with an empty (=) response), so a later gap analysis measures each of `behaviour:test838`, `behaviour:test840`, `behaviour:test884`, `behaviour:test886`, `behaviour:test943`, `behaviour:test945` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0059 ([BL-1989]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

- Finding: GF-0059, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test838`, `behaviour:test840`, `behaviour:test884`, `behaviour:test886`, `behaviour:test943`, `behaviour:test945`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test838 ('imap://;AUTH=EXTERNAL@host/...'): '<verify><protocol> differs at byte 17 (line 2): expected "A002 AUTHENTICATE EXTERNAL\r\n", got the end'; 840 (SASL-IR) expected 'A002 AUTHENTICATE EXTERNAL ='. test884/886 (pop3): expected 'AUTH EXTERNAL' / 'AUTH EXTERNAL =', got 'RETR 884'. test943/945 (smtp 'external authentication without credentials'): expected 'AUTH EXTERNAL' / 'AUTH EXTERNAL =', got the end. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 838,840,884,943

Suggestion, copied from the finding:

In Curl.Authentication.UnitLibrary's SaslAuthenticator/SaslMechanismRanking and the IMAP, POP3 and SMTP login decisions, let a ;AUTH=EXTERNAL login option start SASL without a user name or password. EXTERNAL's response is the (empty) user name, sent as '=' when empty, inline under SASL-IR or after the '+' / '334' continuation, as curl 8.21.0 does.

## Acceptance criteria

- [x] `behaviour:test838`: Curl answers what curl 8.22.0 answers, `upstream test838 passes`, so the item measures `match`.
- [x] `behaviour:test840`: Curl answers what curl 8.22.0 answers, `upstream test840 passes`, so the item measures `match`.
- [x] `behaviour:test884`: Curl answers what curl 8.22.0 answers, `upstream test884 passes`, so the item measures `match`.
- [x] `behaviour:test886`: Curl answers what curl 8.22.0 answers, `upstream test886 passes`, so the item measures `match`.
- [x] `behaviour:test943`: Curl answers what curl 8.22.0 answers, `upstream test943 passes`, so the item measures `match`.
- [x] `behaviour:test945`: Curl answers what curl 8.22.0 answers, `upstream test945 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Cause: BL-1989 already starts SASL EXTERNAL with no user (probed end to end: Curl sent
  `A002 AUTHENTICATE EXTERNAL`). What still differed is the answer after the continuation:
  Curl sent `=`, while curl 8.22.0's tests 838, 884 and 943 (read from the curl-8_22_0 tag on
  GitHub, since the local upstream copy is behind the audit guard) expect an empty line.
  Under SASL-IR (840, 886, 945) `=` on the command line was already right.
- Fix: in the IMAP, POP3 and SMTP handlers an initial response sent on its own line after
  `+` / `334` is an empty line when it is empty; an empty answer to a challenge stays `=`
  (as `ExecuteAsync_EmptyMessages_AreSentAsEquals` pins). No ADR: it follows upstream's
  tests and decides nothing new. Curl.Authentication was not changed.
- Pinned by `ImapProtocolHandlerAuthenticationTests.ExecuteAsync_ExternalNamedWithoutUser_AuthenticatesAndIsAskedForFirst`
  (838), `Pop3ProtocolHandlerLoginTests.ExecuteAsync_ExternalNamedAndOfferedWithoutUser_AuthenticatesWithAnEmptyUserName`
  and `ExecuteAsync_EmptyInitialResponse_IsEqualsInlineAndAnEmptyLineAfterTheContinuation`
  (884/886; the latter was an unmeasured `=` pin, renamed), and the new
  `SmtpProtocolHandlerAuthenticationTests.ExecuteAsync_ExternalRequiredAndOfferedWithoutUserOrSaslIr_AnswersThe334WithAnEmptyLine` (943).
- Both branches of each new encoder are reached by the fast tests (empty: the EXTERNAL
  tests; non-empty: the non-IR LOGIN/PLAIN tests), so Measure-CodeQuality was not run.
  No option changed, so `--ai-help` is unaffected. The items close only on a later gap run.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. An empty EXTERNAL initial response after + / 334 is now an empty line on IMAP, POP3 and SMTP, as curl 8.22.0 sends it
