---
id: GF-0064
title: --crlf does not convert an SMTP upload's LF line ends to CR LF
area: behaviour
key: behaviour:smtp-upload-ignores-crlf
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-10_0657
closed:
regression: false
items: [behaviour:test941]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console, Curl.Console.UnitTests]
task:
tasks: []
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

## Log

- 2026-10-10_0657: Opened by gap-behaviour.
