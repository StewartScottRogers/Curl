---
id: GF-0056
title: --netrc-optional lets the netrc password override the URL's, and a .netrc holding a NUL byte stops the transfer
area: behaviour
platform: windows
key: behaviour:netrc-url-credentials-and-nul-byte
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-10_0657
closed: 2026-10-11_0936
regression: false
items: [behaviour:test381, behaviour:test793]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
task: BL-1986
tasks: [BL-1986]
---
# GF-0056 - --netrc-optional lets the netrc password override the URL's, and a .netrc holding a NUL byte stops the transfer

## Summary

Curl differs from upstream curl in behaviour: --netrc-optional lets the netrc password override the URL's, and a .netrc holding a NUL byte stops the transfer.

## Evidence

Every item expects 'upstream test<N> passes'. test381 (ftp://mary:drfrank@... --netrc-optional): '<verify><protocol> differs at byte 16 (line 2): expected "PASS drfrank\r\n", got "PASS yram\r\n"'. test793 (.netrc with an embedded NUL and a quoted token): expected 'USER username', got the end. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 381,793

## Suggestion

In Curl.Console's TransferCredentialLookup, under --netrc-optional, keep a password the URL gives and take only what the URL lacks from .netrc, as curl 8.21.0 does. In Curl.Authentication.UnitLibrary's NetrcTokenScanner/NetrcFile, read a .netrc holding a NUL byte as curl does, ending the token or line there and carrying on with the quoted-token rules, rather than failing the lookup.

## Measurements

- 2026-10-10_0657: 2 of 2 items are gaps.
- 2026-10-11_0936: 0 of 2 items are gaps.
- 2026-10-11_1950: 0 of 2 items are gaps.
- 2026-10-12_0951: 0 of 2 items are gaps.
- 2026-10-12_2042: 0 of 2 items are gaps.
- 2026-10-13_1406: 0 of 2 items are gaps.
- 2026-10-14_0904: 0 of 2 items are gaps.
- 2026-10-15_0304: 0 of 2 items are gaps.
- 2026-10-15_0543: 0 of 2 items are gaps.
- 2026-10-15_0751: 0 of 2 items are gaps.
- 2026-10-15_1657: 0 of 2 items are gaps.
- 2026-10-17_2225: 0 of 2 items are gaps.
- 2026-10-19_0804: 0 of 2 items are gaps.
- 2026-10-19_1329: 0 of 2 items are gaps.
- 2026-10-22_0148: 0 of 2 items are gaps.
- 2026-10-23_1637: 0 of 2 items are gaps.
- 2026-10-25_1542: 0 of 2 items are gaps.
- 2026-10-30_2103: 0 of 2 items are gaps.

## Log

- 2026-10-10_0657: Opened by gap-behaviour.
- 2026-10-10_0756: Filed BL-1986.
- 2026-10-11_0936: Closed: run 2026-10-11_0936 measured every item as match or excluded.
