---
id: GF-0058
title: -F to an smtp:// or imap:// URL is not sent as a MIME message: SMTP sends VRFY and IMAP sends LIST
area: behaviour
platform: windows
key: behaviour:mail-multipart-form-not-sent
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-10_0657
closed: 2026-10-11_1950
regression: false
items: [behaviour:test1187, behaviour:test646, behaviour:test648, behaviour:test649, behaviour:test647]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests]
task: BL-2081
tasks: [BL-1988, BL-2081]
---
# GF-0058 - -F to an smtp:// or imap:// URL is not sent as a MIME message: SMTP sends VRFY and IMAP sends LIST

## Summary

Curl differs from upstream curl in behaviour: -F to an smtp:// or imap:// URL is not sent as a MIME message: SMTP sends VRFY and IMAP sends LIST.

## Evidence

Every item expects 'upstream test<N> passes'. test646 (smtp with -F parts and --mail-from/--mail-rcpt): '<verify><protocol> differs at byte 10 (line 2): expected "MAIL FROM:<sender@example.com>\r\n", got "VRFY recipient@example.com\r\n"'; 648 and 1187 are the same. test649 (-F ...;encoder=7bit with an 8-bit file): expected 'EHLO 649', got the end. test647 (imap APPEND from -F): expected 'A003 APPEND 647 (\\Seen) {940}', got 'A003 LIST "647" *'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 646,649,647

## Suggestion

In Curl.Console's mail mapping (MailRequestOptionsMapping), treat -F parts on an smtp:// or imap:// URL as the upload, as curl 8.21.0 does. Build the MIME message (multipart/mixed with nested multipart/alternative, ;headers=, ;encoder=) with the same multipart writer HTTP uses, but with mail headers and no Content-Length. Send it through SmtpMailTransaction or ImapAppend. A part whose encoder cannot carry its bytes (7bit with 8-bit data) fails before EHLO, with curl's exit code.

## Measurements

- 2026-10-10_0657: 5 of 5 items are gaps.
- 2026-10-11_0936: 4 of 5 items are gaps.
- 2026-10-11_1950: 0 of 5 items are gaps.
- 2026-10-12_0951: 0 of 5 items are gaps.
- 2026-10-12_2042: 0 of 5 items are gaps.
- 2026-10-13_1406: 0 of 5 items are gaps.
- 2026-10-14_0904: 0 of 5 items are gaps.
- 2026-10-15_0304: 0 of 5 items are gaps.
- 2026-10-15_0543: 0 of 5 items are gaps.
- 2026-10-15_0751: 0 of 5 items are gaps.
- 2026-10-15_1657: 0 of 5 items are gaps.
- 2026-10-17_2225: 0 of 5 items are gaps.
- 2026-10-19_0804: 0 of 5 items are gaps.
- 2026-10-19_1329: 0 of 5 items are gaps.
- 2026-10-22_0148: 0 of 5 items are gaps.
- 2026-10-23_1637: 0 of 5 items are gaps.
- 2026-10-25_1542: 0 of 5 items are gaps.
- 2026-10-30_2103: 0 of 5 items are gaps.

## Log

- 2026-10-10_0657: Opened by gap-behaviour.
- 2026-10-10_0756: Filed BL-1988.
- 2026-10-11_1029: Filed BL-2081.
- 2026-10-11_1950: Closed: run 2026-10-11_1950 measured every item as match or excluded.
