---
id: GF-0061
title: POP3 with CRAM-MD5 as the only offered mechanism sends no AUTH at all
area: behaviour
key: behaviour:pop3-cram-md5-not-attempted
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-10_0657
closed:
regression: false
items: [behaviour:test891]
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
task:
tasks: []
---
# GF-0061 - POP3 with CRAM-MD5 as the only offered mechanism sends no AUTH at all

## Summary

Curl differs from upstream curl in behaviour: POP3 with CRAM-MD5 as the only offered mechanism sends no AUTH at all.

## Evidence

behaviour:test891 (pop3 -u user:secret, CAPA offering only CRAM-MD5, its challenge answered with a bare LF) expected 'upstream test891 passes', actual '<verify><protocol> differs at byte 6 (line 2): expected "AUTH CRAM-MD5\r\n", got the end'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 891

## Suggestion

In Curl.Protocol.Pop3.UnitLibrary's Pop3Login, send AUTH CRAM-MD5 when CAPA offers it and credentials are given, as curl 8.21.0 does. Read a continuation that ends in a bare LF as a reply line, then fail as curl does.

## Measurements

- 2026-10-10_0657: 1 of 1 items are gaps.

## Log

- 2026-10-10_0657: Opened by gap-behaviour.
