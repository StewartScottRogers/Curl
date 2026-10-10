---
id: GF-0062
title: SMTP AUTH PLAIN over two URLs with -: sends an empty authorization identity; upstream expects user, user, password
area: behaviour
key: behaviour:smtp-plain-message-on-connection-reuse
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-10_0657
closed:
regression: false
items: [behaviour:test938]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
task:
tasks: []
---
# GF-0062 - SMTP AUTH PLAIN over two URLs with -: sends an empty authorization identity; upstream expects user, user, password

## Summary

Curl differs from upstream curl in behaviour: SMTP AUTH PLAIN over two URLs with -: sends an empty authorization identity; upstream expects user, user, password.

## Evidence

behaviour:test938 (two smtp URLs joined by -:, -u user.one:secret then user.two:secret) expected 'upstream test938 passes', actual '<verify><protocol> differs at byte 25 (line 3): expected "dXNlci5vbmUAdXNlci5vbmUAc2VjcmV0\r\n", got "AHVzZXIub25lAHNlY3JldA==\r\n"': curl sends 'user.one NUL user.one NUL secret' and Curl sends 'NUL user.one NUL secret'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 938

## Suggestion

Measure test938's command line against the reference with Record-CurlExchange.ps1 -Smtp to confirm when curl 8.21.0 fills PLAIN's authorization identity with the user name. Then make Curl.Authentication.UnitLibrary's PLAIN message (and Curl.Protocol.Smtp.UnitLibrary's SmtpSaslAuthentication) build it the same way, keeping the empty identity where test833-style cases expect it.

## Measurements

- 2026-10-10_0657: 1 of 1 items are gaps.

## Log

- 2026-10-10_0657: Opened by gap-behaviour.
