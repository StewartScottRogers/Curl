---
id: BL-1990
title: Close GF-0060: A SASL exchange given a bad challenge (CRAM-MD5 rubbish, a broken NTLM type-2) is not cancelled with * and retried with the next mechanism
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2026]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1990 — Close GF-0060: A SASL exchange given a bad challenge (CRAM-MD5 rubbish, a broken NTLM type-2) is not cancelled with * and retried with the next mechanism

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0060 (A SASL exchange given a bad challenge (CRAM-MD5 rubbish, a broken NTLM type-2) is not cancelled with * and retried with the next mechanism), so a later gap analysis measures each of `behaviour:test833`, `behaviour:test879`, `behaviour:test935`, `behaviour:test834`, `behaviour:test880`, `behaviour:test936` as `match`.

## Context

- Finding: GF-0060, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test833`, `behaviour:test879`, `behaviour:test935`, `behaviour:test834`, `behaviour:test880`, `behaviour:test936`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test833 (imap, CRAM-MD5 challenged with 'Rubbish'): '<verify><protocol> differs at byte 45 (line 3): expected "*\r\n", got the end'; 879 (pop3) and 935 (smtp) are the same. test834 (imap, AUTH NTLM PLAIN, a broken type-2): expected 'TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=', got the end; 880 (pop3) and 936 (smtp) are the same. Upstream then expects AUTHENTICATE PLAIN. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 833,834,879,880,935,936

Suggestion, copied from the finding:

In Curl.Authentication.UnitLibrary (ChallengeSaslExchange, SecurityContextSaslExchange) and the IMAP/POP3/SMTP authentication loops (ImapAuthentication, Pop3Login, SmtpSaslAuthentication), treat a challenge the mechanism cannot decode as a cancel: send '*', read the reply, and go on to the next offered mechanism (PLAIN here), as curl 8.21.0's SASL_CANCEL and downgrade do. Rather than end the session, send the NTLM type-1 after the server's '+' even when PLAIN is also offered.

## Acceptance criteria

- [ ] `behaviour:test833`: Curl answers what curl 8.21.0 answers, `upstream test833 passes`, so the item measures `match`.
- [ ] `behaviour:test879`: Curl answers what curl 8.21.0 answers, `upstream test879 passes`, so the item measures `match`.
- [ ] `behaviour:test935`: Curl answers what curl 8.21.0 answers, `upstream test935 passes`, so the item measures `match`.
- [ ] `behaviour:test834`: Curl answers what curl 8.21.0 answers, `upstream test834 passes`, so the item measures `match`.
- [ ] `behaviour:test880`: Curl answers what curl 8.21.0 answers, `upstream test880 passes`, so the item measures `match`.
- [ ] `behaviour:test936`: Curl answers what curl 8.21.0 answers, `upstream test936 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- 2026-10-10 (lane 5): Curl already cancels and downgrades. Replayed against this tree's Curl (`curl.exe` from 192ec6f80) with `Record-CurlExchange.ps1 -Script`, the server sending what each case's `<servercmd>` lists:
  - test833 shape (IMAP, `+ Rubbish` to CRAM-MD5): `A002 AUTHENTICATE CRAM-MD5`, `*`, `A003 AUTHENTICATE PLAIN`, `AHVzZXIAc2VjcmV0`, `A004 SELECT 833` - as upstream expects.
  - test834 shape (IMAP NTLM, `+`, then `+ Rubbish`): Type 1, `*`, `A003 AUTHENTICATE PLAIN`, PLAIN message, `SELECT`. The Type 1 is SSPI's on Windows (as curl's Schannel build sends; these cases are `!SSPI`); off Windows the hand-built NTLM sends the expected `TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=` (`HandBuiltSecurityContextFactoryTests`, `CurlCompositionSmtpNtlmTests`).
  - test879 shape (POP3 CRAM-MD5) and test936 shape (SMTP NTLM, `334 Rubbish`): `*`, then `AUTH PLAIN` and the PLAIN message, then `RETR` / `MAIL FROM`.
  - The same paths are pinned by `ImapProtocolHandlerSaslCancelTests`, `Pop3ProtocolHandlerSaslCancelTests` and `SmtpProtocolHandlerSaslCancelTests` (BL-1220 and its POP3/SMTP twins).
- So "got the end" right after `AUTHENTICATE`/`AUTH` comes from the gap harness, which lanes may not read (ADR-0433 guard). All six cases give a quoted command with an argument (`REPLY "AUTHENTICATE CRAM-MD5" + Rubbish`); a harness that does not match those would never send the `+`/`334`. Filed BL-2026 (interactive only) to find the cause in the harness, and parked this task behind it. No code changed.


## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Backlog. Waits on BL-2026: Curl already cancels with * and downgrades to PLAIN on a loopback replay of all six cases; the 'got the end' comes from the gap harness, which only an interactive session may read
