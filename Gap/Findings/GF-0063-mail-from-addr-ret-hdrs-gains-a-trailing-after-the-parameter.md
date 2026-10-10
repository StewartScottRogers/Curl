---
id: GF-0063
title: --mail-from '<addr> RET=HDRS' gains a trailing '>' after the parameters; curl sends an address that starts with '<' as given
area: behaviour
key: behaviour:smtp-mail-from-with-parameters-rewrapped
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-10_0657
closed:
regression: false
items: [behaviour:test3215]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
task: BL-1993
tasks: [BL-1993]
---
# GF-0063 - --mail-from '<addr> RET=HDRS' gains a trailing '>' after the parameters; curl sends an address that starts with '<' as given

## Summary

Curl differs from upstream curl in behaviour: --mail-from '<addr> RET=HDRS' gains a trailing '>' after the parameters; curl sends an address that starts with '<' as given.

## Evidence

behaviour:test3215 (--mail-from "<sender@example.com> RET=HDRS") expected 'upstream test3215 passes', actual '<verify><protocol> differs at byte 50 (line 2): expected "MAIL FROM:<sender@example.com> RET=HDRS\r\n", got "MAIL FROM:<sender@example.com> RET=HDRS>\r\n"'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 3215

## Suggestion

In Curl.Protocol.Smtp.UnitLibrary's SmtpMailTransaction, send a --mail-from (and --mail-rcpt) value that already starts with '<' as given, adding no closing '>', as curl 8.21.0's smtp.c does, so DSN parameters (RET=, NOTIFY=) survive.

## Measurements

- 2026-10-10_0657: 1 of 1 items are gaps.

## Log

- 2026-10-10_0657: Opened by gap-behaviour.
- 2026-10-10_0756: Filed BL-1993.
