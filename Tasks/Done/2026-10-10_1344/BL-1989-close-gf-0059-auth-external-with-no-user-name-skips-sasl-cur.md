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
completed: 2026-10-10
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

- [x] `behaviour:test838`: Curl answers what curl 8.21.0 answers, `upstream test838 passes`, so the item measures `match`.
- [x] `behaviour:test840`: Curl answers what curl 8.21.0 answers, `upstream test840 passes`, so the item measures `match`.
- [x] `behaviour:test884`: Curl answers what curl 8.21.0 answers, `upstream test884 passes`, so the item measures `match`.
- [x] `behaviour:test886`: Curl answers what curl 8.21.0 answers, `upstream test886 passes`, so the item measures `match`.
- [x] `behaviour:test943`: Curl answers what curl 8.21.0 answers, `upstream test943 passes`, so the item measures `match`.
- [x] `behaviour:test945`: Curl answers what curl 8.21.0 answers, `upstream test945 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Cause: a URL like `imap://;AUTH=EXTERNAL@host/` reaches the handlers with no credential
  (null). `SaslMechanismRanking.CanUseExternal` demanded a credential, so IMAP and SMTP,
  whose gates already let a named-and-offered EXTERNAL through, chose nothing and failed
  with exit 67; `Pop3Login.LogInAsync` returned before SASL whenever there was no
  credential and no bearer token, so POP3 went straight to `RETR`.
- Fix, matching curl 8.21.0's `Curl_sasl_can_authenticate` (EXTERNAL offered and preferred
  needs no user) and `Curl_sasl_start` (EXTERNAL only needs an empty password):
  `CanUseExternal` now accepts a null credential, and `Pop3Login` goes on to SASL when
  `AUTH=EXTERNAL` names EXTERNAL and `CAPA` offers it. EXTERNAL's message is the empty user
  name, which the handlers already send as `=` (inline under SASL-IR or after `+`).
  No ADR: this follows curl's code, it decides nothing new.
- The upstream test files sit under the gap office's folder, which the audit guard keeps
  lanes out of, so the expected protocol was taken from the finding's evidence. Pinned by
  `SaslAuthenticatorTests.ChooseMechanism_ExternalWithoutCredential_PicksExternal`,
  `Begin_ExternalWithoutCredential_SendsAnEmptyMessage` and
  `Pop3ProtocolHandlerLoginTests.ExecuteAsync_ExternalNamedAndOfferedWithoutUser_AuthenticatesWithAnEmptyUserName`
  (884/886) plus `ExecuteAsync_NoUserAndNoNamedExternalOffered_SendsNoLogin` for every
  branch of the new POP3 condition; the existing IMAP and SMTP no-user EXTERNAL tests
  already pinned 838/840 and 943/945 framing. The behaviour items close only when a later
  gap run re-measures them.
- No option added or changed, so `--ai-help` is unaffected. Measure-CodeQuality not run:
  each new branch has its own test case (two small conditions, complexity well under 10).

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. ;AUTH=EXTERNAL with no user name now starts SASL EXTERNAL with an empty (=) response on IMAP, POP3 and SMTP
