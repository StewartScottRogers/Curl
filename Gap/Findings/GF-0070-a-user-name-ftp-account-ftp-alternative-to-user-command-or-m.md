---
id: GF-0070
title: A user name, FTP account, --ftp-alternative-to-user command or --mail-from address holding CR LF is sent to the server instead of failing with exit 43
area: behaviour
platform: windows
key: behaviour:crlf-in-protocol-command-argument-not-rejected
severity: Critical
status: closed
scope: target
introduced-in:
opened: 2026-10-13_1406
closed: 2026-10-14_0904
regression: false
items: [behaviour:test2110, behaviour:test2114, behaviour:test2115, behaviour:test2116]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
task: BL-2162
tasks: [BL-2162]
---
# GF-0070 - A user name, FTP account, --ftp-alternative-to-user command or --mail-from address holding CR LF is sent to the server instead of failing with exit 43

## Summary

Curl differs from upstream curl in behaviour: A user name, FTP account, --ftp-alternative-to-user command or --mail-from address holding CR LF is sent to the server instead of failing with exit 43.

## Evidence

Each item expects 'upstream test<N> passes': exit 43 (CURLE_BAD_FUNCTION_ARGUMENT) with the injected command never reaching the server. Actual: test2114 (a -K file with user = 'anonymous<CR><LF>STOR: /2114'): '<verify><protocol> differs at byte 0 (line 1): expected the end, got USER anonymous<CR><LF>' (upstream rejects the credentials before connecting); test2115 (ftp-account = 'one<CR><LF>DELE 2115', server replies 332 to PASS): 'differs at byte 38 (line 3): expected the end, got ACCT one<CR><LF>'; test2116 (ftp-alternative-to-user = 'USER me<CR><LF>DELE 2116', server 530 to USER): 'differs at byte 16 (line 2): expected the end, got USER me<CR><LF>'; test2110 (mail-from = 'sender@example.com<CR><LF>DATA', -T upload): 'differs at byte 11 (line 2): expected QUIT<CR><LF>, got MAIL FROM:<sender@example.com<CR><LF>'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- $env:LOCALAPPDATA/Curl/gap/upstream/8.22.0/tests/data $env:TEMP/raw.json 2110,2114,2115,2116

## Suggestion

Reject a CR or LF in any value Curl writes into a protocol command line with exit 43 (CurlExitCode.BadFunctionArgument), as curl 8.22.0 does. In Curl.Protocol.Ftp.UnitLibrary (FtpSession): check the user name and password before connecting (test2114: nothing is sent), the --ftp-account value before ACCT is built (test2115: USER and PASS are sent, then the transfer fails) and the --ftp-alternative-to-user command before it is sent (test2116). In Curl.Protocol.Smtp.UnitLibrary (SmtpMailTransaction): check the --mail-from address (and each --mail-rcpt and --mail-auth value the same way) before MAIL FROM is sent, ending the session with QUIT (test2110). Add unit tests pinning the exit code and that no injected command is written.

## Measurements

- 2026-10-13_1406: 4 of 4 items are gaps.
- 2026-10-14_0904: 0 of 4 items are gaps.
- 2026-10-15_0304: 0 of 4 items are gaps.
- 2026-10-15_0543: 0 of 4 items are gaps.
- 2026-10-15_0751: 0 of 4 items are gaps.
- 2026-10-15_1657: 0 of 4 items are gaps.
- 2026-10-17_2225: 0 of 4 items are gaps.
- 2026-10-19_0804: 0 of 4 items are gaps.
- 2026-10-19_1329: 0 of 4 items are gaps.
- 2026-10-22_0148: 0 of 4 items are gaps.
- 2026-10-23_1637: 0 of 4 items are gaps.
- 2026-10-25_1542: 0 of 4 items are gaps.
- 2026-10-30_2103: 0 of 4 items are gaps.
- 2026-10-31_1828: 0 of 4 items are gaps.

## Log

- 2026-10-13_1406: Opened by gap-behaviour.
- 2026-10-13_1457: Filed BL-2162.
- 2026-10-14_0904: Closed: run 2026-10-14_0904 measured every item as match or excluded.
