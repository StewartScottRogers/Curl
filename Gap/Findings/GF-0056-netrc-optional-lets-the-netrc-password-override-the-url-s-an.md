---
id: GF-0056
title: --netrc-optional lets the netrc password override the URL's, and a .netrc holding a NUL byte stops the transfer
area: behaviour
key: behaviour:netrc-url-credentials-and-nul-byte
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-10_0657
closed:
regression: false
items: [behaviour:test381, behaviour:test793]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
task:
tasks: []
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

## Log

- 2026-10-10_0657: Opened by gap-behaviour.
