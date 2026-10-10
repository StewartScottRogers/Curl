---
id: GF-0055
title: A reply pipelined behind STARTTLS/AUTH TLS is not refused with exit 8, and a pre-authenticated FTP server skips AUTH under --ssl-reqd
area: behaviour
key: behaviour:starttls-pipelined-reply-and-preauth
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-10_0657
closed:
regression: false
items: [behaviour:test980, behaviour:test982, behaviour:test983, behaviour:test986]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
task: BL-1985
tasks: [BL-1985]
---
# GF-0055 - A reply pipelined behind STARTTLS/AUTH TLS is not refused with exit 8, and a pre-authenticated FTP server skips AUTH under --ssl-reqd

## Summary

Curl differs from upstream curl in behaviour: A reply pipelined behind STARTTLS/AUTH TLS is not refused with exit 8, and a pre-authenticated FTP server skips AUTH under --ssl-reqd.

## Evidence

Every item expects 'upstream test<N> passes'. test980 (SMTP, STARTTLS answered '454' with more replies pipelined): '<verify><protocol> differs at byte 20 (line 3): expected the end, got "AUTH PLAIN AHVzZXIAc2VjcmV0\r\n"'; upstream expects exit 8. test983 (FTP, AUTH answered with pipelined lines): expected the end, got 'AUTH TLS'; upstream expects exit 8. test982 (POP3 STARTTLS pipelined): 'curl did not finish within 20 seconds'. test986 (welcome '230', --ssl-reqd): expected 'AUTH SSL', got 'PWD'; upstream expects exit 64. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 980,982,983,986

## Suggestion

In Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Pop3.UnitLibrary and Curl.Protocol.Ftp.UnitLibrary, after sending STARTTLS/STLS/AUTH, fail with exit 8 when more bytes are already buffered behind its reply (a pipelined server response), as curl 8.21.0 does, and send nothing more. In FtpSession, when the server greets with 230 (pre-authenticated) and --ssl-reqd is set, still send AUTH SSL / AUTH TLS and fail with exit 64 when both are refused.

## Measurements

- 2026-10-10_0657: 4 of 4 items are gaps.

## Log

- 2026-10-10_0657: Opened by gap-behaviour.
- 2026-10-10_0756: Filed BL-1985.
