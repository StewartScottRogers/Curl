---
id: BL-3004
title: Answer DIGEST-MD5's rspauth with an empty line in curl's own form so upstream cases 823 and 869 pass
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary/SaslAuthenticator.cs, Curl.Authentication.UnitLibrary/SaslDigestMd5.cs, Curl.Authentication.UnitTests/SaslAuthenticatorTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Authentication.UnitLibrary/ChallengeSaslExchange.cs, Curl.Protocol.Abstractions.UnitLibrary/ISaslExchange.cs, Curl.Protocol.Abstractions.UnitTests/ISaslExchangeTests.cs, Curl.Protocol.Imap.UnitLibrary/ImapAuthentication.cs, Curl.Protocol.Imap.UnitTests/ImapProtocolHandlerAuthenticationTests.cs, Curl.Protocol.Imap.UnitTests/Fakes/FakeSaslAuthenticator.cs, Curl.Protocol.Pop3.UnitLibrary/Pop3Login.cs, Curl.Protocol.Pop3.UnitTests/Pop3ProtocolHandlerLoginTests.cs, Curl.Protocol.Pop3.UnitTests/Fakes/ScriptedSaslAuthenticator.cs, Curl.Protocol.Smtp.UnitLibrary/SmtpSaslAuthentication.cs, Curl.Protocol.Smtp.UnitTests/SmtpProtocolHandlerAuthenticationTests.cs, Curl.Protocol.Smtp.UnitTests/Fakes/FakeSaslAuthenticator.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-3004 — Answer DIGEST-MD5's rspauth with an empty line in curl's own form so upstream cases 823 and 869 pass

## Goal

DIGEST-MD5's answer to the server's `rspauth` goes out as an empty line, as curl sends it, so upstream cases 823 and 869 pass.

## Context

Found by BL-2834 (ADR-0751): with Debug and the forced cnonce, test823 and test869 differ at line 4 of <verify><protocol>: curl answers the rspauth challenge with an empty line, Curl sends "=". Run with $env:CURL_UPSTREAM_CASE='823'. Add a focused unit test in Curl.Authentication.UnitTests (ADR-0482).

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

- 2026-10-21: The `=` came from the IMAP, POP3 and SMTP handlers, which sent every empty answer as `=`. curl's `build_message` sends "no message" (DIGEST-MD5's `rspauth` answer) as an empty line and only an explicit empty response (LOGIN's empty password) as `=`. Fix (ADR-0753): a default member `ISaslExchange.SendsLastAnswerAsEmptyLine`, `true` on `ChallengeSaslExchange`, read by each handler's new `EncodeAnswer`. `SaslAuthenticator.cs` and `SaslDigestMd5.cs` needed no change.
- Touches widened to the contract, `ChallengeSaslExchange` and the three handlers with their tests and fakes: no task in Doing on origin/work/dark-factory (BL-3000, BL-3001, BL-3003) names any of them.
- Verified: cases 823 and 869 pass with `CURL_UPSTREAM_CASE` and are on the ratchet; fast tests green for Abstractions (909), Authentication (916), Imap (513), Pop3 (358), Smtp (373) and Conformance (4005); Measure-CodeQuality for the five changed libraries: 0 failing members. Case 907 (SMTP) now sends the empty line but still differs on quote escaping, left to BL-3005.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-21: Doing -> Done. DIGEST-MD5's rspauth answer goes out as an empty line on IMAP, POP3 and SMTP; upstream cases 823 and 869 pass and are on the ratchet
