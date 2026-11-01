---
id: GF-0064
title: --crlf does not convert an SMTP upload's LF line ends to CR LF
area: behaviour
platform: windows
key: behaviour:smtp-upload-ignores-crlf
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-10_0657
closed: 2026-10-11_0936
regression: false
items: [behaviour:test941]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console, Curl.Console.UnitTests]
task: BL-1994
tasks: [BL-1994]
---
# GF-0064 - --crlf does not convert an SMTP upload's LF line ends to CR LF

## Summary

Curl differs from upstream curl in behaviour: --crlf does not convert an SMTP upload's LF line ends to CR LF.

## Evidence

behaviour:test941 (smtp -T upload --crlf) expected 'upstream test941 passes', actual '<verify><upload> differs at byte 15 (line 1): expected "From: different\r\n", got "From: different\n"'. Curl.Protocol.Smtp.UnitLibrary and the mail options have no --crlf setting. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 941

## Suggestion

Carry --crlf into MailRequestOptions (Curl.Console's MailRequestOptionsMapping, Curl.Protocol.Abstractions.UnitLibrary). In Curl.Protocol.Smtp.UnitLibrary, convert each bare LF of the upload to CR LF before dot-stuffing (SmtpDotStuffer), as curl 8.21.0 does.

## Measurements

- 2026-10-10_0657: 1 of 1 items are gaps.
- 2026-10-11_0936: 0 of 1 items are gaps.
- 2026-10-11_1950: 0 of 1 items are gaps.
- 2026-10-12_0951: 0 of 1 items are gaps.
- 2026-10-12_2042: 0 of 1 items are gaps.
- 2026-10-13_1406: 0 of 1 items are gaps.
- 2026-10-14_0904: 0 of 1 items are gaps.
- 2026-10-15_0304: 0 of 1 items are gaps.
- 2026-10-15_0543: 0 of 1 items are gaps.
- 2026-10-15_0751: 0 of 1 items are gaps.
- 2026-10-15_1657: 0 of 1 items are gaps.
- 2026-10-17_2225: 0 of 1 items are gaps.
- 2026-10-19_0804: 0 of 1 items are gaps.
- 2026-10-19_1329: 0 of 1 items are gaps.
- 2026-10-22_0148: 0 of 1 items are gaps.
- 2026-10-23_1637: 0 of 1 items are gaps.
- 2026-10-25_1542: 0 of 1 items are gaps.
- 2026-10-30_2103: 0 of 1 items are gaps.
- 2026-10-31_1828: 0 of 1 items are gaps.

## Log

- 2026-10-10_0657: Opened by gap-behaviour.
- 2026-10-10_0756: Filed BL-1994.
- 2026-10-11_0936: Closed: run 2026-10-11_0936 measured every item as match or excluded.
