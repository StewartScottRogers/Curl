---
id: GF-0060
title: A SASL exchange given a bad challenge (CRAM-MD5 rubbish, a broken NTLM type-2) is not cancelled with * and retried with the next mechanism
area: behaviour
platform: windows
key: behaviour:sasl-downgrade-after-bad-challenge
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-10_0657
closed: 2026-10-11_0936
regression: false
items: [behaviour:test833, behaviour:test879, behaviour:test935, behaviour:test834, behaviour:test880, behaviour:test936]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
task: BL-1990
tasks: [BL-1990]
---
# GF-0060 - A SASL exchange given a bad challenge (CRAM-MD5 rubbish, a broken NTLM type-2) is not cancelled with * and retried with the next mechanism

## Summary

Curl differs from upstream curl in behaviour: A SASL exchange given a bad challenge (CRAM-MD5 rubbish, a broken NTLM type-2) is not cancelled with * and retried with the next mechanism.

## Evidence

Every item expects 'upstream test<N> passes'. test833 (imap, CRAM-MD5 challenged with 'Rubbish'): '<verify><protocol> differs at byte 45 (line 3): expected "*\r\n", got the end'; 879 (pop3) and 935 (smtp) are the same. test834 (imap, AUTH NTLM PLAIN, a broken type-2): expected 'TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=', got the end; 880 (pop3) and 936 (smtp) are the same. Upstream then expects AUTHENTICATE PLAIN. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 833,834,879,880,935,936

## Suggestion

In Curl.Authentication.UnitLibrary (ChallengeSaslExchange, SecurityContextSaslExchange) and the IMAP/POP3/SMTP authentication loops (ImapAuthentication, Pop3Login, SmtpSaslAuthentication), treat a challenge the mechanism cannot decode as a cancel: send '*', read the reply, and go on to the next offered mechanism (PLAIN here), as curl 8.21.0's SASL_CANCEL and downgrade do. Rather than end the session, send the NTLM type-1 after the server's '+' even when PLAIN is also offered.

## Measurements

- 2026-10-10_0657: 6 of 6 items are gaps.
- 2026-10-11_0936: 0 of 6 items are gaps.
- 2026-10-11_1950: 0 of 6 items are gaps.
- 2026-10-12_0951: 0 of 6 items are gaps.
- 2026-10-12_2042: 0 of 6 items are gaps.
- 2026-10-13_1406: 0 of 6 items are gaps.
- 2026-10-14_0904: 0 of 6 items are gaps.
- 2026-10-15_0304: 0 of 6 items are gaps.
- 2026-10-15_0543: 0 of 6 items are gaps.
- 2026-10-15_0751: 0 of 6 items are gaps.
- 2026-10-15_1657: 0 of 6 items are gaps.
- 2026-10-17_2225: 0 of 6 items are gaps.
- 2026-10-19_0804: 0 of 6 items are gaps.
- 2026-10-19_1329: 0 of 6 items are gaps.
- 2026-10-22_0148: 0 of 6 items are gaps.
- 2026-10-23_1637: 0 of 6 items are gaps.
- 2026-10-25_1542: 0 of 6 items are gaps.
- 2026-10-30_2103: 0 of 6 items are gaps.

## Log

- 2026-10-10_0657: Opened by gap-behaviour.
- 2026-10-10_0756: Filed BL-1990.
- 2026-10-11_0936: Closed: run 2026-10-11_0936 measured every item as match or excluded.
