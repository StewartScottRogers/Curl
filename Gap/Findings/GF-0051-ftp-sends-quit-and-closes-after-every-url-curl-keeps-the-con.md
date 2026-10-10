---
id: GF-0051
title: FTP sends QUIT and closes after every URL; curl keeps the control connection for the next URL and sends QUIT only at exit
area: behaviour
key: behaviour:ftp-control-connection-not-reused
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-10_0657
closed:
regression: false
items: [behaviour:test1010, behaviour:test1096, behaviour:test1149, behaviour:test1217, behaviour:test1225, behaviour:test146, behaviour:test149, behaviour:test215, behaviour:test216, behaviour:test698, behaviour:test2002, behaviour:test2003]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Console, Curl.Console.UnitTests]
task:
tasks: []
---
# GF-0051 - FTP sends QUIT and closes after every URL; curl keeps the control connection for the next URL and sends QUIT only at exit

## Summary

Curl differs from upstream curl in behaviour: FTP sends QUIT and closes after every URL; curl keeps the control connection for the next URL and sends QUIT only at exit.

## Evidence

Every item expects 'upstream test<N> passes'. test215 (two URLs, same directory): '<verify><protocol> differs at byte 89 (line 10): expected "EPSV\r\n", got "QUIT\r\n"'; 1010, 1096, 216 and 698 are the same. test146 and 1225: expected 'CWD /', got 'QUIT'. test1217: expected 'CWD /this/is/the/path', got 'QUIT'. test2002 (http, ftp, file, tftp): expected 'opcode = 1', got 'QUIT', because curl's QUIT comes at exit, after the TFTP request. test2003: expected 'USER anonymous', got 'GET /20030001 HTTP/1.1'. Cause: Curl.Protocol.Ftp.UnitLibrary/FtpSession.QuitAsync runs at the end of every transfer, and the FTP control connection never enters the run's ConnectionCache. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 215,146,1217,2002

## Suggestion

In Curl.Protocol.Ftp.UnitLibrary, keep an FTP control connection open after a transfer and hand it to the run's connection cache (Curl.Networking.UnitLibrary ConnectionCache, as HTTP does under ADR-0050), keyed by host, port, user and TLS. Track its working directory, so the next URL on it sends 'CWD /' or only the CWDs that differ (singlecwd, multicwd, nocwd), as curl 8.21.0's ftp.c does. Send QUIT when the cache closes the connection at exit. Pin the sequences of test146, 215 and 1217 in Curl.Protocol.Ftp.UnitTests.

## Measurements

- 2026-10-10_0657: 12 of 12 items are gaps.

## Log

- 2026-10-10_0657: Opened by gap-behaviour.
