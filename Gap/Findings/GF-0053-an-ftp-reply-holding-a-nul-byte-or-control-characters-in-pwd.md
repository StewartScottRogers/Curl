---
id: GF-0053
title: An FTP reply holding a NUL byte or control characters (in PWD's path) is accepted; curl ends with exit 8
area: behaviour
key: behaviour:ftp-control-reply-not-validated
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-10_0657
closed:
regression: false
items: [behaviour:test3217, behaviour:test3218, behaviour:test2108]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
task:
tasks: []
---
# GF-0053 - An FTP reply holding a NUL byte or control characters (in PWD's path) is accepted; curl ends with exit 8

## Summary

Curl differs from upstream curl in behaviour: An FTP reply holding a NUL byte or control characters (in PWD's path) is accepted; curl ends with exit 8.

## Evidence

Every item expects 'upstream test<N> passes'. test3217/3218 (PWD answered '257' with a path holding byte 0x03): '<verify><protocol> differs at byte 43 (line 4): expected the end, got "EPSV\r\n"'; upstream expects exit 8. test2108 (PASS answered '230 logged' NUL ' in'): expected the end, got 'PWD'; upstream expects exit 8. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 3217,3218,2108

## Suggestion

In Curl.Protocol.Ftp.UnitLibrary's control-reply reader, fail with exit 8 (weird server reply) on a reply line holding a NUL byte. In the PWD reply parser, refuse a path holding control characters with exit 8, sending nothing more, as curl 8.21.0 does.

## Measurements

- 2026-10-10_0657: 3 of 3 items are gaps.

## Log

- 2026-10-10_0657: Opened by gap-behaviour.
